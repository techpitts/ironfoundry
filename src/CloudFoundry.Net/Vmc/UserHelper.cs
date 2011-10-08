﻿namespace CloudFoundry.Net.Vmc
{
    using Newtonsoft.Json.Linq;
    using Properties;
    using RestSharp;

    internal class UserHelper : BaseVmcHelper
    {
        public UserHelper(VcapCredentialManager credMgr) : base(credMgr) { }

        public VcapClientResult Login(string argEmail, string argPassword)
        {
            VcapClientResult rv;

            var r = new VcapJsonRequest(credMgr, Method.POST, Constants.USERS_PATH, argEmail, "tokens");
            r.AddBody(new { password = argPassword });
            RestResponse response = r.Execute();
            if (response.Content.IsNullOrEmpty())
            {
                rv = new VcapClientResult(false, Resources.Vmc_NoContentReturned_Text);
            }
            else
            {
                var parsed = JObject.Parse(response.Content);
                string token = parsed.Value<string>("token");
                credMgr.RegisterToken(token);
                rv = new VcapClientResult();
            }

            return rv;
        }
    }
}