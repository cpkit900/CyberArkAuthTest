using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using RestSharp;

namespace CyberArkAuthApp
{
    public partial class MainForm : Form
    {
        private string _baseUrl;
        private string _idToken;
        private string _pamSessionToken;  // CyberArk session token obtained from PAM SAML logon
        private string _detectedPamHost;  // Last auto-discovered PAM host
        private readonly CookieContainer _cookieContainer = new CookieContainer();

        public MainForm()
        {
            InitializeComponent();
            cmbMode.Items.AddRange(new string[] { "OIDC", "SAML" });
            cmbMode.SelectedIndex = 1; // Default to SAML
            
            cmbDeploymentType.Items.AddRange(new string[] { "Cloud", "On-Premise" });
            cmbDeploymentType.SelectedIndex = 0; // Default to Cloud
            cmbDeploymentType.SelectedIndexChanged += CmbDeploymentType_SelectedIndexChanged;
        }

        private void CmbDeploymentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isCloud = cmbDeploymentType.SelectedItem?.ToString() == "Cloud";
            
            // Toggle Cloud fields
            label1.Visible = txtTenant.Visible = isCloud;
            label4.Visible = txtPamTenant.Visible = isCloud;
            label2.Visible = txtEmail.Visible = isCloud;

            // Toggle On-Premise fields
            labelPvwaUrl.Visible = txtPvwaUrl.Visible = !isCloud;
        }

        private void Log(string message) =>
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        private async void btnStart_Click(object sender, EventArgs e)
        {
            try {
                Log("Starting Unified Authentication Flow...");
                if (cmbDeploymentType.SelectedItem?.ToString() == "Cloud") {
                    _baseUrl = $"https://{txtTenant.Text}.id.cyberark.cloud";
                }
                await StartCyberArkAuth();
            }
            catch (Exception ex) {
                Log($"Initialization Error: {ex.Message}");
            }
        }

        private async Task StartCyberArkAuth()
        {
            bool isCloud = cmbDeploymentType.SelectedItem?.ToString() == "Cloud";
            string redirectUrl = "";
            string privilegeCloudHost = "";

            if (isCloud)
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
                    Log($"POST URL: {url}");

                    var client = new RestClient(_baseUrl);
                    var request = new RestRequest("/Security/StartAuthentication", Method.Post);
                    var bodyJson = JsonSerializer.Serialize(new { User = txtEmail.Text, Version = "1.0" });
                    request.AddStringBody(bodyJson, DataFormat.Json);

                    var response = await client.ExecuteAsync(request);
                    var json = response.Content ?? "";

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var authResponse = JsonSerializer.Deserialize<IdentityAuthResponse>(json, options);

                    if (!string.IsNullOrEmpty(authResponse?.Result?.PodFqdn))
                    {
                        Log($"PodFqdn received: {authResponse.Result.PodFqdn}. Switching URL...");
                        _baseUrl = $"https://{authResponse.Result.PodFqdn}";
                        redirectNeeded = true;
                        continue;
                    }

                    try {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Result", out var resultEl)) {
                            if (resultEl.TryGetProperty("IdpRedirectUrl", out var idpUrl)) {
                                redirectUrl = idpUrl.GetString() ?? "";
                            }
                            else if (resultEl.TryGetProperty("Challenges", out var challenges) && challenges.GetArrayLength() > 0) {
                                var prompt = challenges[0].GetProperty("Mechanisms")[0].GetProperty("Prompt").GetString();
                                if (!string.IsNullOrEmpty(prompt) && prompt.StartsWith("http"))
                                    redirectUrl = prompt;
                            }
                        }
                    } catch (Exception ex) {
                        Log($"Error parsing response: {ex.Message}");
                    }
                } // End Cloud while loop

                if (string.IsNullOrEmpty(redirectUrl)) {
                    Log("No Redirect URL found from Identity. Is the user federated?");
                    return;
                }
                
                privilegeCloudHost = await DiscoverPamHostAsync();
                Log($"Discovered PAM Host: {privilegeCloudHost}");
            }
            else
            {
                // On-Premise Flow
                string rawPvwaInput = txtPvwaUrl.Text.Trim();
                
                // Clean the input to just get the host, in case the user typed "https://host/"
                if (rawPvwaInput.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    rawPvwaInput = rawPvwaInput.Substring(7);
                if (rawPvwaInput.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    rawPvwaInput = rawPvwaInput.Substring(8);
                
                rawPvwaInput = rawPvwaInput.TrimEnd('/');

                privilegeCloudHost = rawPvwaInput;
                Log($"Step 1: On-Premise mode selected. Targeting PVWA directly at {privilegeCloudHost}");
                
                // For On-Premise, we point the WebView2 directly to the PVWA SAML logon endpoint
                // which will automatically trigger the IdP redirect.
                redirectUrl = $"https://{privilegeCloudHost}/PasswordVault/v10/logon/saml";
            }

            // Launch Auth Form Common Flow
            if (!string.IsNullOrEmpty(redirectUrl)) {
                Log($"Opening Auth Form for: {redirectUrl}");

                    using (var authForm = new AuthForm(redirectUrl, chkUseToken.Checked, privilegeCloudHost))
                    {
                        if (authForm.ShowDialog() == DialogResult.OK)
                        {
                            _idToken = authForm.IdToken;

                            // Import cookies from WebView2 into our CookieContainer if requested
                            if (chkUseCookies.Checked)
                            {
                                Log("Importing Cookies from WebView2...");
                                foreach (var cookie in authForm.AuthCookies)
                                {
                                    try {
                                        string domain = cookie.Domain;
                                        string uriHost = domain.TrimStart('.');
                                        var cookieUri = new Uri($"https://{uriHost}");

                                        var netCookie = new System.Net.Cookie(cookie.Name, cookie.Value)
                                        {
                                            Domain = domain,
                                            Path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path,
                                            Secure = cookie.IsSecure,
                                            HttpOnly = cookie.IsHttpOnly
                                        };

                                        _cookieContainer.Add(cookieUri, netCookie);
                                    } catch (Exception ex) {
                                        Log($"Skipped cookie {cookie.Name}: {ex.Message}");
                                    }
                                }
                            }

                            // Detect actual PAM host from cookies or use discovered host
                            string detectedPamHost = null;
                            foreach (var cookie in authForm.AuthCookies)
                            {
                                if (cookie.Name.Equals("PreferredCulture", StringComparison.OrdinalIgnoreCase) ||
                                    cookie.Domain.Contains("-pcloud.cyberark.cloud", StringComparison.OrdinalIgnoreCase))
                                {
                                    detectedPamHost = cookie.Domain.TrimStart('.');
                                    break;
                                }
                            }

                            if (string.IsNullOrEmpty(detectedPamHost))
                                detectedPamHost = privilegeCloudHost;

                            _detectedPamHost = detectedPamHost;
                            Log($"Using PAM Host: {_detectedPamHost}");

                            // Token mode: exchange the intercepted SAML assertion for a PAM session token
                            if (chkUseToken.Checked && !string.IsNullOrEmpty(authForm.SamlResponse))
                            {
                                Log("Intercepted SAML assertion. Exchanging for PAM session token...");
                                _pamSessionToken = await ExchangeSamlForSessionToken(_detectedPamHost, authForm.SamlResponse);
                                if (!string.IsNullOrEmpty(_pamSessionToken))
                                    Log("PAM session token obtained successfully!");
                                else
                                    Log("Warning: No PAM session token returned — API calls may fail.");
                            }

                            await FetchAccounts(_detectedPamHost);
                        }
                        else
                        {
                            Log("Authentication was cancelled or failed.");
                        }
                    }
                } else {
                    Log("No Redirect URL found. Is the user federated?");
                }
        }

        private async Task FetchAccounts(string detectedPamHost = null)
        {
            Log("Step 2: Accessing Privilege Cloud Accounts API...");

            string pamHost = !string.IsNullOrEmpty(detectedPamHost)
                ? detectedPamHost
                : $"{txtPamTenant.Text}.privilegecloud.cyberark.cloud";

            string pamUrl = $"https://{pamHost}/PasswordVault/API/Accounts?limit=10";
            Log($"Attempting GET: {pamUrl}");

            try {
                var options = new RestClientOptions($"https://{pamHost}");
                
                if (chkUseCookies.Checked) {
                    options.CookieContainer = _cookieContainer;
                    Log("Using Cookies for Auth.");
                }

                var client = new RestClient(options);
                var request = new RestRequest("/PasswordVault/API/Accounts", Method.Get);
                request.AddQueryParameter("limit", "10");

                if (chkUseToken.Checked && !string.IsNullOrEmpty(_pamSessionToken)) {
                    request.AddHeader("Authorization", $"CyberArk {_pamSessionToken}");
                    Log("Using CyberArk Session Token for Auth.");
                }

                var response = await client.ExecuteAsync(request);
                HandleAccountsResponse(response.StatusCode, response.Content ?? "");
            } catch (Exception ex) {
                Log($"API Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers the correct PAM host by probing ISPSS (-pcloud) then legacy (.privilegecloud) URL patterns.
        /// </summary>
        private async Task<string> DiscoverPamHostAsync()
        {
            string tenant = txtPamTenant.Text.Trim();

            // Try ISPSS (Shared Services) format first: {tenant}-pcloud.cyberark.cloud
            string ispsHost = $"{tenant}-pcloud.cyberark.cloud";
            // Try legacy format: {tenant}.privilegecloud.cyberark.cloud
            string legacyHost = $"{tenant}.privilegecloud.cyberark.cloud";

            Log($"Probing PAM hosts: {ispsHost} and {legacyHost}...");

            foreach (var host in new[] { ispsHost, legacyHost })
            {
                try {
                    var client = new RestClient($"https://{host}");
                    var req = new RestRequest("/PasswordVault/v10/logon", Method.Get);
                    req.Timeout = TimeSpan.FromSeconds(5);
                    var resp = await client.ExecuteAsync(req);
                    // Any response (even 401/redirect) means the host exists
                    if (resp.ResponseStatus == RestSharp.ResponseStatus.Completed)
                    {
                        Log($"Discovered live PAM host: {host}");
                        return host;
                    }
                } catch { /* Host not reachable, try next */ }
            }

            Log($"Could not auto-discover PAM host. Using: {legacyHost}");
            return legacyHost;
        }

        /// <summary>
        /// Exchanges a SAML assertion for a CyberArk PAM session token via SAML Logon API.
        /// Returns the session token string, or null on failure.
        /// </summary>
        private async Task<string> ExchangeSamlForSessionToken(string pamHost, string samlResponse)
        {
            try {
                var client = new RestClient($"https://{pamHost}");
                var request = new RestRequest("/PasswordVault/API/auth/SAML/Logon", Method.Post);
                // SAML logon expects form-encoded body
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("SAMLResponse", samlResponse);

                var response = await client.ExecuteAsync(request);
                string content = response.Content ?? "";

                Log($"SAML Logon response ({response.StatusCode}): {content.Substring(0, Math.Min(200, content.Length))}");

                if (response.StatusCode == HttpStatusCode.OK && !content.TrimStart().StartsWith("<"))
                {
                    // Session token is returned as a raw quoted string: "sessiontoken123"
                    return content.Trim().Trim('"');
                }
            } catch (Exception ex) {
                Log($"SAML Logon Error: {ex.Message}");
            }
            return null;
        }


        private void HandleAccountsResponse(HttpStatusCode statusCode, string content)
        {
            // If we got HTML back, the server redirected us to a login page - auth failed
            if (content.TrimStart().StartsWith("<"))
            {
                Log($"API Error: Server returned a login/HTML page (HTTP {(int)statusCode}). "
                  + "For ISPSS tenants, use Cookie mode instead of Token mode.");
                return;
            }

            Log($"Response ({statusCode}): {content.Substring(0, Math.Min(500, content.Length))}");

            if (statusCode == HttpStatusCode.OK) {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<AccountResponse>(content, options);
                this.Invoke(new MethodInvoker(() => {
                    dgvAccounts.DataSource = data?.value;
                    Log($"Success! Displaying {data?.value?.Count ?? 0} accounts.");
                }));
            } else {
                Log($"API Failed ({statusCode}).");
            }
        }
    }
}
