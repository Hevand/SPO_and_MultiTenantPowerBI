using Microsoft.PowerBI.Api.V2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sample.ProviderHosted.WebParts.PowerBI.Models
{
    public class EmbedConfig
    {

        private bool DiagnosticsEnabled = false;
        public EmbedConfig(bool diagnosticsEnabled)
        {
            this.DiagnosticsEnabled = diagnosticsEnabled;
            this.Diagnostics = new List<string>();
        }

        public string Id { get; set; }
        public string EmbedUrl { get; set; }
        public EmbedToken EmbedToken { get; set; }
               
        public int MinutesToExpiration
        {
            get
            {
                TimeSpan minutesToExpiration = EmbedToken.Expiration.Value - DateTime.UtcNow;
                return (int)minutesToExpiration.TotalMinutes;
            }
        }
        public bool? IsEffectiveIdentityRolesRequired { get; set; }
        public bool? IsEffectiveIdentityRequired { get; set; }
        public bool EnableRLS { get; set; }
        public string Username { get; set; }
        public string Roles { get; set; }
        public string ErrorMessage { get; internal set; }

        public List<string> Diagnostics { get; internal set; }

        internal void Trace(object sender, string message, int indent = 0)
        {
            if (DiagnosticsEnabled)
            {
                string ws = "----------";
                string classname = "";
                if (sender != null)
                {
                    classname = sender.GetType().Name;
                }

                Diagnostics.Add($"[{classname}] {ws.Substring(0, indent)}{message}");
            }
        }
    }
}