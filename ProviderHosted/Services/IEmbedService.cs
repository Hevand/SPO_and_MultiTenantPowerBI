using Sample.ProviderHosted.WebParts.PowerBI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Sample.ProviderHosted.WebParts.PowerBI.Services
{
    public interface IEmbedService
    {
        EmbedConfig EmbedConfig { get; }
        Task<bool> EmbedReport(string userName, string roles, string customdata);        
    }
}