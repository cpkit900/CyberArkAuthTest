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

        public AuthForm(string startUrl)
        {
            InitializeComponent();
            _startUrl = startUrl;
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
                // Instead of getting cookies just for the current URL, 
                // we should get ALL cookies for the root domain or we might miss .privilegecloud cookies
                // It's safer to just extract the domains we care about
                var cookieManager = webView.CoreWebView2.CookieManager;
                
                // Get ALL cookies for the webview profile
                // The GetCookiesAsync without URI gets all cookies for the profile
                var cookies = await cookieManager.GetCookiesAsync(null);

                bool idTokenFound = false;
                AuthCookies.Clear();

                foreach (var cookie in cookies)
                {
                    AuthCookies.Add(cookie); // Captures for identity AND privilegecloud
                    if (cookie.Name.StartsWith("idToken-", StringComparison.OrdinalIgnoreCase))
                    {
                        IdToken = cookie.Value;
                        idTokenFound = true;
                    }
                }

                // We need to make sure we don't close prematurely before idToken is found.
                // The URL might end up being a vanity URL or .cyberark.cloud without .id.
                // If we found an idToken and we are on a cyberark.cloud domain, we are likely done.
                if (idTokenFound)
                {
                    FinalUrl = currentUrl;
                    DialogResult = DialogResult.OK; // Sets DialogResult and signals FormClosing
                    Close();
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
