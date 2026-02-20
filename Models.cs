using System.Collections.Generic;

namespace CyberArkAuthApp
{
    public class IdentityAuthResponse {
        public bool success { get; set; }
        public IdentityResult Result { get; set; }
        public string Message { get; set; }
    }

    public class IdentityResult {
        public string Token { get; set; }
        public string PodFqdn { get; set; }
    }

    public class AccountResponse {
        public List<AccountValue> value { get; set; }
    }

    public class AccountValue {
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Address { get; set; }
        public string PlatformID { get; set; }
    }
}
