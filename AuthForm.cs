using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace CyberArkAuthApp
{
    public partial class AuthForm : Form
    {
        public string IdToken { get; private set; }
        public string SamlResponse { get; private set; }      // For token mode
        public string PamLogonUrl { get; private set; }       // The PAM SAML logon URL intercepted
        public List<CoreWebView2Cookie> AuthCookies { get; private set; } = new List<CoreWebView2Cookie>();
        public string FinalUrl { get; private set; }

        private readonly string _startUrl;
        private readonly bool _useToken;
        private readonly string _privilegeCloudHost;

        // State tracking: have we already triggered the PAM navigation?
        private bool _navigatedToPam = false;

        public AuthForm(string startUrl, bool useToken, string privilegeCloudHost)
        {
            InitializeComponent();
            _startUrl = startUrl;
            _useToken = useToken;
            _privilegeCloudHost = privilegeCloudHost;
            this.Load += AuthForm_Load;
            this.FormClosing += AuthForm_FormClosing;
        }

        private async void AuthForm_Load(object sender, EventArgs e)
        {
            try {
                await webView.EnsureCoreWebView2Async();
                webView.NavigationCompleted += WebView_NavigationCompleted;

                // Intercept ALL web requests so we can capture the SAML assertion when it's POSTed to PAM
                webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;

                webView.CoreWebView2.Navigate(_startUrl);
            } catch (Exception ex) {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Intercept web requests to capture the SAML assertion being POST-ed to the PAM logon endpoint.
        /// </summary>
        private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try {
                string requestUrl = e.Request.Uri;

                // Look for the PAM SAML logon endpoint
                if (requestUrl.Contains("/PasswordVault/", StringComparison.OrdinalIgnoreCase) &&
                    requestUrl.Contains("/Logon", StringComparison.OrdinalIgnoreCase) &&
                    e.Request.Method == "POST")
                {
                    PamLogonUrl = requestUrl;

                    // Read the POST body to capture the SAML response
                    var content = e.Request.Content;
                    if (content != null)
                    {
                        using var reader = new StreamReader(content);
                        string body = reader.ReadToEnd();

                        // Extract SAMLResponse parameter from form body: SAMLResponse=<value>
                        const string key = "SAMLResponse=";
                        int idx = body.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            string rawValue = body.Substring(idx + key.Length);
                            // Value may end at & or end of string
                            int ampIdx = rawValue.IndexOf('&');
                            if (ampIdx >= 0) rawValue = rawValue.Substring(0, ampIdx);
                            SamlResponse = Uri.UnescapeDataString(rawValue);
                        }
                    }
                }
            } catch { /* Don't crash on interception errors */ }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            string currentUrl = webView.Source.ToString();

            // Only process cyberark.cloud URLs
            if (!currentUrl.Contains("cyberark.cloud", StringComparison.OrdinalIgnoreCase))
                return;

            // Collect ALL cookies from the WebView profile
            var cookieManager = webView.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync(null);

            bool idTokenFound = false;
            AuthCookies.Clear();
            foreach (var cookie in cookies)
            {
                AuthCookies.Add(cookie);
                if (cookie.Name.StartsWith("idToken-", StringComparison.OrdinalIgnoreCase))
                {
                    IdToken = cookie.Value;
                    idTokenFound = true;
                }
            }

            // ─── TOKEN MODE ───────────────────────────────────────────────────────────
            // In token mode, we also need the SAML authentication to go through so the
            // SAMLResponse gets intercepted. We close as soon as we have both idToken
            // and the SAML assertion was captured (or we're on the PAM domain).
            if (_useToken && idTokenFound)
            {
                bool onPamDomain = IsPamDomain(currentUrl);

                if (!_navigatedToPam && !onPamDomain)
                {
                    // Navigate to PAM to trigger SAML SSO → captures SamlResponse via WebResourceRequested
                    _navigatedToPam = true;
                    string pamUrl = $"https://{_privilegeCloudHost}/PasswordVault/v10/";
                    webView.CoreWebView2.Navigate(pamUrl);
                    return;
                }

                // If we got the SAML response already, or we're on PAM domain, we're done
                if (!string.IsNullOrEmpty(SamlResponse) || onPamDomain)
                {
                    FinalUrl = currentUrl;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }

            // ─── COOKIE MODE ──────────────────────────────────────────────────────────
            if (!_useToken)
            {
                bool onPamDomain = IsPamDomain(currentUrl);

                if (onPamDomain)
                {
                    bool stillOnLoginPage =
                        currentUrl.Contains("/logon", StringComparison.OrdinalIgnoreCase) ||
                        currentUrl.Contains("/Login", StringComparison.OrdinalIgnoreCase) ||
                        currentUrl.Contains("SharedServices", StringComparison.OrdinalIgnoreCase) ||
                        currentUrl.Contains("AzureActiveDirectory", StringComparison.OrdinalIgnoreCase);

                    if (!stillOnLoginPage && currentUrl.Contains("/PasswordVault", StringComparison.OrdinalIgnoreCase))
                    {
                        FinalUrl = currentUrl;
                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }
                }
                else if (idTokenFound && !_navigatedToPam)
                {
                    _navigatedToPam = true;
                    string pamUrl = $"https://{_privilegeCloudHost}/PasswordVault/v10/";
                    webView.CoreWebView2.Navigate(pamUrl);
                }
            }
        }

        private static bool IsPamDomain(string url) =>
            url.Contains("privilegecloud.cyberark.cloud", StringComparison.OrdinalIgnoreCase)
            || url.Contains("-pcloud.cyberark.cloud", StringComparison.OrdinalIgnoreCase);

        private void AuthForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
                DialogResult = DialogResult.Cancel;
        }
    }
}
