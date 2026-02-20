using System;
using System.Collections.Generic;
using System.IO;
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
        private string _sessionToken;
        private readonly HttpClient _httpClient = new HttpClient();

        public MainForm()
        {
            InitializeComponent();
            cmbMode.Items.AddRange(new string[] { "OIDC", "SAML" });
            cmbMode.SelectedIndex = 0;
        }

        private void Log(string message) =>
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        private async void btnStart_Click(object sender, EventArgs e)
        {
            try {
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

            // Filter to intercept the authentication callback
            webView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://*.id.cyberark.cloud/*", CoreWebView2WebResourceContext.All);

            webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
        }

        private async Task StartCyberArkAuth()
        {
            Log("Step 1: Contacting CyberArk StartAuthentication...");
            string url = $"https://{txtTenant.Text}.id.cyberark.cloud/Security/StartAuthentication";
            var payload = new { User = txtEmail.Text, Version = "1.0" };
            var jsonPayload = JsonSerializer.Serialize(payload);
            
            Log($"POST URL: {url}");
            Log($"Payload: {jsonPayload}");

            var response = await _httpClient.PostAsync(url, 
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
            
            var json = await response.Content.ReadAsStringAsync();
            Log($"Response: {json}");

            using var doc = JsonDocument.Parse(json);
            
            string redirectUrl = "";
            try {
                if (cmbMode.Text == "OIDC")
                    redirectUrl = doc.RootElement.GetProperty("Result").GetProperty("IdpRedirectUrl").GetString();
                else
                    redirectUrl = doc.RootElement.GetProperty("Result").GetProperty("Challenges")[0].GetProperty("Mechanisms")[0].GetProperty("Prompt").GetString();
            } catch (Exception ex) {
                 Log($"Error parsing StartAuthentication response: {ex.Message}");
                 // Keep going to see if we can do anything else or just let it fail later
            }

            if (!string.IsNullOrEmpty(redirectUrl)) {
                Log("Redirecting WebView2 to IDP...");
                webView.CoreWebView2.Navigate(redirectUrl);
            } else {
                Log("No Redirect URL found in response.");
            }
        }

        private async void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try {
                string uri = e.Request.Uri;

                // Intercept OIDC Code (GET request query string)
                if (cmbMode.Text == "OIDC" && uri.Contains("code=")) {
                    Log($"OIDC Code Intercepted from URL: {uri}");
                    var query = new Uri(uri).Query.TrimStart('?');
                    var code = GetQueryParameter(query, "code");
                    Log($"Extracted Code: {code}");
                    
                    e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 200, "OK", "");
                    await ExchangeCredential(code);
                }
                // Intercept SAML Response (POST request body)
                else if (cmbMode.Text == "SAML" && e.Request.Method == "POST" && uri.Contains("/auth/saml/callback")) {
                    Log("SAML Callback Intercepted from POST body.");
                    using var reader = new StreamReader(e.Request.Content);
                    var body = await reader.ReadToEndAsync();
                    Log($"SAML Body: {body}");
                    var saml = GetQueryParameter(body, "SAMLResponse");
                    await ExchangeCredential(saml);
                }
            } catch (Exception ex) {
                Log($"Error in OnWebResourceRequested: {ex.Message}");
            }
        }

        private string GetQueryParameter(string queryString, string key)
        {
            if (string.IsNullOrEmpty(queryString)) return null;
            foreach (var pair in queryString.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                {
                    var k = System.Net.WebUtility.UrlDecode(parts[0]);
                    if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return System.Net.WebUtility.UrlDecode(parts[1]);
                    }
                }
            }
            return null;
        }

        private async Task ExchangeCredential(string credential)
        {
            Log("Step 2: Exchanging for Bearer Token via Identity API...");
            string url = $"https://{txtTenant.Text}.id.cyberark.cloud/Security/AdvanceAuthentication";
            
            var key = cmbMode.Text == "OIDC" ? "Code" : "SAMLResponse";
            var payload = new Dictionary<string, string> { { key, credential } };
            var jsonPayload = JsonSerializer.Serialize(payload);

            Log($"POST URL: {url}");
            Log($"Payload: {jsonPayload}");

            var response = await _httpClient.PostAsync(url, 
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
            
            var responseString = await response.Content.ReadAsStringAsync();
            Log($"Response: {responseString}");

            var result = JsonSerializer.Deserialize<IdentityAuthResponse>(responseString);

            if (result?.success == true) {
                _sessionToken = result.Result.Token;
                Log("Authentication Successful. Session Token acquired.");
                await FetchAccounts();
            } else {
                Log($"Authentication Failed: {result?.Message}");
                if (result?.Result != null) {
                   // Log more details if available structure allows, but result.Result is just Token in current model.
                   // Extending model might be needed if there are other fields.
                }
            }
        }

        private async Task FetchAccounts()
        {
            Log("Step 3: Accessing Privilege Cloud Accounts API...");
            string url = $"https://{txtTenant.Text}.privilegecloud.cyberark.cloud/PasswordVault/API/Accounts?limit=10";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_sessionToken}");

            try {
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<AccountResponse>(response);
                
                this.Invoke(new MethodInvoker(() => {
                    dgvAccounts.DataSource = data.value;
                    Log($"Displaying {data.value.Count} accounts in grid.");
                }));
            } catch (Exception ex) {
                Log($"API Error: {ex.Message}");
            }
        }
    }
}
