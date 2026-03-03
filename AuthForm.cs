using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace CyberArkAuthApp
{
    public partial class AuthForm : Form
    {
        public string IdToken { get; private set; }
        public List<CoreWebView2Cookie> AuthCookies { get; private set; } = new List<CoreWebView2Cookie>();
        public string FinalUrl { get; private set; }
        
        private string _startUrl;
        private bool _useToken;

        public AuthForm(string startUrl, bool useToken)
        {
            InitializeComponent();
            _startUrl = startUrl;
            _useToken = useToken;
            this.Load += AuthForm_Load;
            
            // Allow manual close to cancel
            this.FormClosing += AuthForm_FormClosing;
        }

        private async void AuthForm_Load(object sender, EventArgs e)
        {
            try {
                await webView.EnsureCoreWebView2Async();
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.Navigate(_startUrl);
            } catch (Exception ex) {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error");
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            string currentUrl = webView.Source.ToString();
            
            if (currentUrl.Contains("cyberark.cloud", StringComparison.OrdinalIgnoreCase))
            {
                var cookieManager = webView.CoreWebView2.CookieManager;
                
                // Get ALL cookies for the webview profile
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

                // If the user checked "Use Token", we don't need Privilege Cloud cookies at all!
                // We just need the idToken.
                if (_useToken && idTokenFound)
                {
                    FinalUrl = currentUrl;
                    DialogResult = DialogResult.OK; 
                    Close();
                    return;
                }

                if (!_useToken)
                {
                    // If we need Privilege Cloud cookies, we need them to be in the Vault
                    if (currentUrl.Contains("privilegecloud", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if they moved past the logon page into the actual vault application
                        // e.g., /PasswordVault/v10/Accounts or /PasswordVault/api/
                        if (currentUrl.Contains("/v10", StringComparison.OrdinalIgnoreCase) && 
                            !currentUrl.Contains("logon", StringComparison.OrdinalIgnoreCase) &&
                            !currentUrl.Contains("SharedServices", StringComparison.OrdinalIgnoreCase))
                        {
                            FinalUrl = currentUrl;
                            DialogResult = DialogResult.OK; 
                            Close();
                            return;
                        }
                    }
                    else if (idTokenFound)
                    {
                        // If we have idToken but are still on Identity domain, automatically bounce them to the vault
                        string host = new Uri(currentUrl).Host;
                        if (host.EndsWith(".id.cyberark.cloud", StringComparison.OrdinalIgnoreCase))
                        {
                            string tenant = host.Split('.')[0];
                            // The best root url to trigger the ISPSS automatic login flow without hitting the "Shared Services" interstitial
                            // is usually just the root PasswordVault/v10/ url, which handles OIDC auth bounce.
                            string pamUrl = $"https://{tenant}.privilegecloud.cyberark.cloud/PasswordVault/v10/";
                            webView.CoreWebView2.Navigate(pamUrl);
                            return;
                        }
                    }
                }
            }
        }

        private void AuthForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                DialogResult = DialogResult.Cancel;
            }
        }
    }
}
