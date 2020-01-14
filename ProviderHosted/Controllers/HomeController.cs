using Microsoft.SharePoint.Client;
using Microsoft.PowerBI.Api.V2;
using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using Sample.ProviderHosted.WebParts.PowerBI.Services;

namespace Sample.ProviderHosted.WebParts.PowerBI.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEmbedService m_embedService = new EmbedService();
                
        [SharePointContextFilter]
        public async Task<ActionResult> Index()
        {
            string username = string.Empty, roles = string.Empty, customdata = string.Empty;

            m_embedService.EmbedConfig.Trace(this, "Index - GetUserDetailsFromSharePoint()...");
            GetUserdetailsFromSharePoint(ref username, ref roles, ref customdata);
            
            m_embedService.EmbedConfig.Trace(this, "Index - EmbedReport()...");
            var embedResult = await m_embedService.EmbedReport(username, roles, customdata);

            if (embedResult)
            {
                return View(m_embedService.EmbedConfig);
            }
           else
           {
                return View(m_embedService.EmbedConfig);
           }
        }

        private void GetUserdetailsFromSharePoint(ref string username, ref string roles, ref string customdata)
        {
            try
            {
                User spUser = null;

                m_embedService.EmbedConfig.Trace(this, $"Getting user details and roles from SharePoint out of the HttpContext");
                
                var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);

                if (spContext == null)
                {
                    m_embedService.EmbedConfig.Trace(this, "Failure in getting the SharePoint context. Return with no result.", 2);
                    return;
                } 

                m_embedService.EmbedConfig.Trace(this, $"SP Context received for Host = {spContext.SPHostUrl} / Web = {spContext.SPAppWebUrl}. Creating user context.", 2);

                using (var clientContext = spContext.CreateUserClientContextForSPHost())
                {
                    if (clientContext != null)
                    {
                        m_embedService.EmbedConfig.Trace(this, $"Client context created.");
                        spUser = clientContext.Web.CurrentUser;

                        clientContext.Load(spUser,
                            user => user.Title,
                            user => user.Email,
                            user => user.UserId);

                        clientContext.Load(spUser.Groups);

                        clientContext.ExecuteQuery();
                        
                        m_embedService.EmbedConfig.Trace(this, $"User context created. Title = {spUser.Title ?? "UNDEFINED"}, Email = {spUser.Email ?? "UNDEFINED"}", 4);

                        username = spUser.Title;

                        int i = 1;
                        foreach (var g in spUser.Groups)
                        {
                            m_embedService.EmbedConfig.Trace(this, $"Group membership [{i++}/{spUser.Groups.Count}]: Title: '{g.Title}', Description: '{g.Description}', PrincipalType: '{g.PrincipalType}', Id: '{g.Id}'", 6);
                        }

                        //TODO: define logic to use the spUser object and SharePoint groups to get the approriate role. 
                        customdata = spUser.Email;

                        m_embedService.EmbedConfig.Trace(this, $"SharePoint authentication completed.", 4);
                    }
                }
            }        
            catch (Exception e)
            {
                m_embedService.EmbedConfig.Trace(this, $"Exception during SharePoint authentication. Message: {e.Message}", 2);
            }    
        }
    }
}
