using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace CyberArkAuthApp
{
    public partial class MainForm : Form
    {
        private string _baseUrl;
        private string _idToken;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer = new CookieContainer();
        private bool _authSuccessHandled = false;

        public MainForm()
        {
            InitializeComponent();
            var handler = new HttpClientHandler() { CookieContainer = _cookieContainer, UseCookies = true };
            _httpClient = new HttpClient(handler);
            cmbMode.Items.AddRange(new string[] { "OIDC", "SAML" });
            cmbMode.SelectedIndex = 0;
        }

        private void Log(string message) =>
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        private async void btnStart_Click(object sender, EventArgs e)
        {
            try {
                _authSuccessHandled = false;
                // Default Base URL
                _baseUrl = $"https://{txtTenant.Text}.id.cyberark.cloud";
                await InitializeBrowser();
                await StartCyberArkAuth();
            }
            catch (Exception ex) {
                Log($"Initialization Error: {ex.Message}");
            }
        }

        private async Task InitializeBrowser()
        {
            await webView.EnsureCoreWebView2Async();
            Log($"Browser Initialized for {cmbMode.Text}.");
            
            // Subscribe to navigation completion to check for login success
            webView.NavigationCompleted -= WebView_NavigationCompleted;
            webView.NavigationCompleted += WebView_NavigationCompleted;
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            string currentUrl = webView.Source.ToString();
            Log($"Navigation Completed: {currentUrl}");

            // Check if we are on a CyberArk domain (relaxed check for vanity URLs)
            if (currentUrl.Contains("cyberark.cloud", StringComparison.OrdinalIgnoreCase) && !_authSuccessHandled)
            {
                await CheckForAuthCookies(currentUrl);
            }
        }

        private async Task CheckForAuthCookies(string url)
        {
            try {
                var cookieManager = webView.CoreWebView2.CookieManager;
                var cookies = await cookieManager.GetCookiesAsync(url);

                Log($"Inspecting cookies for {url}:");
                foreach (var cookie in cookies)
                {
                    Log($"Cookie: {cookie.Name} (Domain: {cookie.Domain})");
                    
                    // CyberArk modern OIDC flow uses an idToken- JWT cookie (not .ASPXAUTH)
                    if (cookie.Name.StartsWith("idToken-", StringComparison.OrdinalIgnoreCase))
                    {
                        _idToken = cookie.Value;
                        Log("idToken JWT found! Login successful.");
                    }
                    
                    // Also add to HttpClient cookie container
                    _cookieContainer.Add(new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
                }

                if (!string.IsNullOrEmpty(_idToken))
                {
                    _authSuccessHandled = true;
                    var uri = new Uri(url);
                    await FetchAccounts(uri.Host);
                }
                else
                {
                    Log("No idToken found yet. Waiting for more navigations...");
                }
            } catch (Exception ex) {
                Log($"Cookie Check Error: {ex.Message}");
            }
        }

        private async Task StartCyberArkAuth()
        {
            Log("Step 1: Contacting CyberArk StartAuthentication...");
            
            bool redirectNeeded = true;
            int maxRedirects = 3;
            int currentRedirect = 0;

            while (redirectNeeded && currentRedirect < maxRedirects)
            {
                redirectNeeded = false;
                currentRedirect++;

                string url = $"{_baseUrl}/Security/StartAuthentication";
                var payload = new { User = txtEmail.Text, Version = "1.0" };
                var jsonPayload = JsonSerializer.Serialize(payload);
                
                Log($"POST URL: {url}");

                // We use a clean client for this initial handshake if we want, or just the main one.
                // Using main one involves cookies which is fine.
                var response = await _httpClient.PostAsync(url, 
                    new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                
                var json = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResponse = JsonSerializer.Deserialize<IdentityAuthResponse>(json, options);

                if (!string.IsNullOrEmpty(authResponse?.Result?.PodFqdn))
                {
                    Log($"PodFqdn received: {authResponse.Result.PodFqdn}. Switching URL...");
                    _baseUrl = $"https://{authResponse.Result.PodFqdn}";
                    redirectNeeded = true;
                    continue; 
                }

                // Parse for Redirect URL (IdP or SAML)
                // If it's pure OIDC/SAML federation, StartAuthentication usually returns the redirect URL directly.
                using var doc = JsonDocument.Parse(json);
                string redirectUrl = "";
                
                try {
                    // Try to get IdpRedirectUrl directly (common for some flows)
                     if (doc.RootElement.TryGetProperty("Result", out var resultEl)) {
                        if (resultEl.TryGetProperty("IdpRedirectUrl", out var idpUrl)) {
                            redirectUrl = idpUrl.GetString();
                        }
                        // Fallback to mechanisms check if not found directly
                        else if (resultEl.TryGetProperty("Challenges", out var challenges) && challenges.GetArrayLength() > 0) {
                            var prompt = challenges[0].GetProperty("Mechanisms")[0].GetProperty("Prompt").GetString();
                            // Sometimes the prompt IS the redirect URL for some mechanisms? 
                            // Or we might need to "Advance" with "Start" mechanism.
                            // But for "IdpRedirectUrl" it specifically means "Go Here".
                            // Assume if we didn't get IdpRedirectUrl, we might have standard logic.
                            // For this specific issue (OIDC/SAML), IdpRedirectUrl is key.
                            
                            if (string.IsNullOrEmpty(redirectUrl) && !string.IsNullOrEmpty(prompt) && prompt.StartsWith("http"))
                            {
                                redirectUrl = prompt;
                            }
                        }
                     }
                } catch (Exception ex) {
                     Log($"Error parsing response: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(redirectUrl)) {
                    Log($"Redirecting WebView2 to: {redirectUrl}");
                    webView.CoreWebView2.Navigate(redirectUrl);
                } else {
                    Log("No Redirect URL found. Is the user federated?");
                }
            }
        }

        private async Task FetchAccounts(string host = null)
        {
            Log("Step 2: Accessing Privilege Cloud Accounts API (via Cookies)...");
            
            // Extract subdomain from identity portal host (e.g., rocketsoftware.cyberark.cloud => rocketsoftware)
            // PAM API lives at <subdomain>.privilegecloud.cyberark.cloud
            string subdomain = txtTenant.Text; // default fallback
            if (!string.IsNullOrEmpty(host))
            {
                // host = e.g. "rocketsoftware.cyberark.cloud"
                subdomain = host.Split('.')[0];
            }

            string targetHost = !string.IsNullOrEmpty(host) ? host : $"{subdomain}.privilegecloud.cyberark.cloud";

            string pamUrl = $"https://{subdomain}.privilegecloud.cyberark.cloud/PasswordVault/API/Accounts?limit=10";
            
            Log($"Attempting GET: {pamUrl}");

            try {
                // Use the idToken JWT as a Bearer token for the API call
                using var request = new HttpRequestMessage(HttpMethod.Get, pamUrl);
                if (!string.IsNullOrEmpty(_idToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _idToken);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                Log($"Response ({response.StatusCode}): {content.Substring(0, Math.Min(500, content.Length))}");

                if (response.IsSuccessStatusCode) {
                     var data = JsonSerializer.Deserialize<AccountResponse>(content);
                     this.Invoke(new MethodInvoker(() => {
                        dgvAccounts.DataSource = data.value;
                        Log($"Success! Displaying {data.value?.Count ?? 0} accounts.");
                    }));
                } else {
                    Log($"API Failed ({response.StatusCode}).");
                }

            } catch (Exception ex) {
                Log($"API Error: {ex.Message}");
            }
        }
    }
}
