using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.PowerBI.Api.V2;
using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Sample.ProviderHosted.WebParts.PowerBI.Models;

namespace Sample.ProviderHosted.WebParts.PowerBI.Services
{
    public class EmbedService : IEmbedService
    {
        private static readonly bool DiagnosticsEnabled = ConfigurationManager.AppSettings["diagnosticsEnabled"] == "true";
        private static readonly string defaultCustomDataForEmbedToken = ConfigurationManager.AppSettings["defaultCustomDataForEmbedToken"];
        private static readonly bool validateThatWebAppIsLaunchedAsSharePointAppPart = ConfigurationManager.AppSettings["validateThatWebAppIsLaunchedAsSharePointAppPart"] == "true";


        private static readonly string AuthorityUrl = ConfigurationManager.AppSettings["authorityUrl"];
        private static readonly string ResourceUrl = ConfigurationManager.AppSettings["resourceUrl"];
        private static readonly string ApplicationId = ConfigurationManager.AppSettings["applicationId"];
        private static readonly string ApiUrl = ConfigurationManager.AppSettings["apiUrl"];
        private static readonly string WorkspaceId = ConfigurationManager.AppSettings["workspaceId"];
        private static readonly string ReportId = ConfigurationManager.AppSettings["reportId"];
                
        private static readonly NameValueCollection sectionConfig = ConfigurationManager.GetSection("ServicePrincipal") as NameValueCollection;
        private static readonly string ApplicationSecret = sectionConfig["applicationSecret"];
        private static readonly string Tenant = sectionConfig["tenantId"];
        private static readonly string ServicePrincipalObjectId = sectionConfig["servicePrincipalObjectId"];
        
        public EmbedConfig EmbedConfig
        {
            get { return m_embedConfig; }
        }

        private EmbedConfig m_embedConfig;
        private TokenCredentials m_tokenCredentials;

        public EmbedService()
        {
            m_tokenCredentials = null;
            m_embedConfig = new EmbedConfig(DiagnosticsEnabled);
            m_embedConfig.Trace(this, "Constructor called", 2);
        }

        public async Task<bool> EmbedReport(string username, string roles, string customdata)
        {
            m_embedConfig.Trace(this, "EmbedReport called. Getting token credentials from SharePoint", 2);

            // Get token credentials for user
            var getCredentialsResult = await GetTokenCredentials();            
            if (!getCredentialsResult)
            {
                m_embedConfig.Trace(this, "Failure to get token credentials.", 4);
                // The error message set in GetTokenCredentials
                return false;
            } else
            {
                m_embedConfig.Trace(this, $"Credentials received successfully.", 4);
            }

            try
            {
                m_embedConfig.Trace(this, $"Creating a PowerBi Client for '{ApiUrl ?? "UNDEFINED"}'", 2);
                // Create a Power BI Client object. It will be used to call Power BI APIs.
                using (var client = new PowerBIClient(new Uri(ApiUrl), m_tokenCredentials))
                {
                    #region Get PowerBI Workspace / Report / Dataset
                    m_embedConfig.Trace(this, $"Query the PowerBi environment for all reports for workspace '{WorkspaceId ?? "UNDEFINED"}'", 4);
                    // Get a list of reports.
                    var reports = await client.Reports.GetReportsInGroupAsync(WorkspaceId);

                    // No reports retrieved for the given workspace.
                    if (reports.Value.Count() == 0)
                    {

                        m_embedConfig.Trace(this, $"UNEXPECTED - No reports were found (or accessible) in {WorkspaceId ?? "UNDEFINED"}. Abort processing", 6);
                        m_embedConfig.ErrorMessage = "No reports were found in the workspace";
                        return false;
                    }

                    Report report;
                    if (string.IsNullOrWhiteSpace(ReportId))
                    {
                        m_embedConfig.Trace(this, $"No report id was configured. Attempt to select the first available report in {WorkspaceId}", 6);
                        // Get the first report in the workspace.
                        report = reports.Value.FirstOrDefault();

                        m_embedConfig.Trace(this, $"First report found: { (report != null ? report.Name : "UNDEFINED") }", 6);
                    }
                    else
                    {
                        m_embedConfig.Trace(this, $"Report Id configured: {ReportId}. Attempt to find this in {WorkspaceId ?? "UNDEFINED"}", 6);

                        report = reports.Value.FirstOrDefault(r => r.Id.Equals(ReportId, StringComparison.InvariantCultureIgnoreCase));

                        m_embedConfig.Trace(this, $"First report found: { (report != null ? report.Name : "UNDEFINED")}", 6);
                    }

                    if (report == null)
                    {
                        m_embedConfig.Trace(this, $"No report was found. Abort further processing.", 6);

                        m_embedConfig.ErrorMessage = "No report with the given ID was found in the workspace. Make sure ReportId is valid.";
                        return false;
                    }


                    m_embedConfig.Trace(this, $"Report found. Attempt to get dataset specifics for this report's dataset {report.DatasetId ?? "UNDEFINED"}", 4);
                    var datasets = await client.Datasets.GetDatasetByIdInGroupAsync(WorkspaceId, report.DatasetId);

                    m_embedConfig.Trace(this, $"Dataset group found, name: {(datasets != null ? datasets.Name : "UNDEFINED")}", 4);
                    m_embedConfig.Trace(this, $"Dataset group properties: IsEffectiveEntityRequired = {datasets.IsEffectiveIdentityRequired}, IsEffectiveIdentityRolesRequired = {datasets.IsEffectiveIdentityRolesRequired}", 4);

                    m_embedConfig.IsEffectiveIdentityRequired = datasets.IsEffectiveIdentityRequired;
                    m_embedConfig.IsEffectiveIdentityRolesRequired = datasets.IsEffectiveIdentityRolesRequired;

                    #endregion
                    
                    #region Generate token to access dataset

                    GenerateTokenRequest generateTokenRequestParameters;


                    if (!validateThatWebAppIsLaunchedAsSharePointAppPart || !string.IsNullOrWhiteSpace(username))
                    {
                        m_embedConfig.Trace(this, $"Generating token request parameters for dataset = '{report.DatasetId ?? "UNDEFINED"}', for User = '{username ?? "UNDEFINED"}', with Roles = '{roles ?? "UNDEFINED"}'", 4);

                        var rls = new EffectiveIdentity(ServicePrincipalObjectId, new List<string> { report.DatasetId })
                        {

                            //for testing purposes, a hardcoded "customdata" value can be provided.
                            CustomData = String.IsNullOrEmpty(defaultCustomDataForEmbedToken)
                            ? customdata
                            : defaultCustomDataForEmbedToken
                        };

                        generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view", identities: new List<EffectiveIdentity> { rls });
                    }
                    else
                    {
                        m_embedConfig.Trace(this, $"Generating generic token request (accessLevel: view) for dataset = '{report.DatasetId ?? "UNDEFINED"}'.", 4);

                        generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");
                    }

                    m_embedConfig.Trace(this, $"Generating token response for report", 2);
                    var tokenResponse = await client.Reports.GenerateTokenInGroupAsync(WorkspaceId, report.Id, generateTokenRequestParameters);

                    if (tokenResponse == null)
                    {
                        m_embedConfig.Trace(this, $"UNEXPECTED - Generating token respose for report did not yield results", 4);
                        m_embedConfig.ErrorMessage = "Failed to generate embed token.";
                        return false;
                    }

                    #endregion

                    // Generate Embed Configuration.
                    m_embedConfig.EmbedToken = tokenResponse;
                    m_embedConfig.EmbedUrl = report.EmbedUrl;
                    m_embedConfig.Id = report.Id;
                }
            }
            catch (HttpOperationException exc)
            {
                m_embedConfig.Trace(this, $"HttpOperationException received. Message: {exc.Message}, StackTrace: {exc.StackTrace}", 2);
                m_embedConfig.ErrorMessage = string.Format("Status: {0} ({1})\r\nResponse: {2}\r\nRequestId: {3}", exc.Response.StatusCode, (int)exc.Response.StatusCode, exc.Response.Content, exc.Response.Headers["RequestId"].FirstOrDefault());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if web.config embed parameters have valid values.
        /// </summary>
        /// <returns>Null if web.config parameters are valid, otherwise returns specific error string.</returns>
        private string GetWebConfigErrors()
        {
            Guid result;
            // Application Id must have a value.
            if (string.IsNullOrWhiteSpace(ApplicationId) || !Guid.TryParse(ApplicationId, out result))
            {
                return "ApplicationId should be a valid GUID object. please register your application as Native app in https://dev.powerbi.com/apps and fill client Id in web.config.";
            }

            // Workspace Id must have a value.
            if (string.IsNullOrWhiteSpace(WorkspaceId) || !Guid.TryParse(WorkspaceId, out result))
            {
                return "WorkspaceId must be a Guid object. Please select a workspace you own and fill its Id in web.config";
            }

            if (string.IsNullOrEmpty(ServicePrincipalObjectId) || !Guid.TryParse(ServicePrincipalObjectId, out result))
            {
                return "ServicePrincipalObjectId must be populated and must be a Guid object";
            }


            if (string.IsNullOrWhiteSpace(ApplicationSecret))
            {
                return "ApplicationSecret is empty. please register your application as Web app and fill appSecret in web.config.";
            }

            // Must fill tenant Id
            if (string.IsNullOrWhiteSpace(Tenant))
            {
                return "Invalid Tenant. Please fill Tenant ID in Tenant under web.config";
            }

            return null;
        }

        private async Task<AuthenticationResult> DoAuthentication()
        {
            AuthenticationResult authenticationResult = null;

            m_embedConfig.Trace(this, $"PowerBI authentication started using service principal");

            // For app only authentication, we need the specific tenant id in the authority url
            var tenantSpecificURL = AuthorityUrl.Replace("common", Tenant);
            var authenticationContext = new AuthenticationContext(tenantSpecificURL);

            // Authentication using app credentials                
            m_embedConfig.Trace(this, $"  Tenant Specific Url: {tenantSpecificURL ?? "UNDEFINED"}");
            m_embedConfig.Trace(this, $"  Application Id: {ApplicationId ?? "UNDEFINED"}");
            m_embedConfig.Trace(this, $"  Application Secret: {ApplicationSecret.Substring(0, 4)}...");

            var credential = new ClientCredential(ApplicationId, ApplicationSecret);
            authenticationResult = await authenticationContext.AcquireTokenAsync(ResourceUrl, credential);
            
            m_embedConfig.Trace(this, $"PowerBI authentication completed.");

            return authenticationResult;
        }

        private async Task<bool> GetTokenCredentials()
        {
            // var result = new EmbedConfig { Username = username, Roles = roles };
            var error = GetWebConfigErrors();
            if (error != null)
            {
                m_embedConfig.Trace(this, $"Problem validating web.config: {error}", 4);
                m_embedConfig.ErrorMessage = error;
                return false;
            }

            AuthenticationResult authenticationResult;
            // Authenticate using created credentials            
            try
            {
                authenticationResult = await DoAuthentication();
            }
            catch (AggregateException exc)
            {

                m_embedConfig.Trace(this, $"Problem authenticating: {exc.InnerException.Message}", 2);
                m_embedConfig.ErrorMessage = exc.InnerException.Message;
                return false;
            }

            if (authenticationResult == null)
            {
                m_embedConfig.Trace(this, "Authentication completed, but failed with an empty result", 2);
                m_embedConfig.ErrorMessage = "Authentication Failed.";
                return false;
            }

            m_tokenCredentials = new TokenCredentials(authenticationResult.AccessToken, "Bearer");
            return true;
        }
    }
}