using System;
using System.Configuration;
using System.Web.Mvc;

namespace Sample.ProviderHosted.WebParts.PowerBI
{
    /// <summary>
    /// SharePoint action filter attribute.
    /// </summary>
    public class SharePointContextFilterAttribute : ActionFilterAttribute
    {
        private static readonly bool validateThatWebAppIsLaunchedAsSharePointAppPart = ConfigurationManager.AppSettings["validateThatWebAppIsLaunchedAsSharePointAppPart"] == "true";

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            if (!validateThatWebAppIsLaunchedAsSharePointAppPart)
            {
                //validation is not required. Consider 'success' and continue execution.
                return;
            }

            Uri redirectUrl;
            switch (SharePointContextProvider.CheckRedirectionStatus(filterContext.HttpContext, out redirectUrl))
            {
                case RedirectionStatus.Ok:
                    return;
                case RedirectionStatus.ShouldRedirect:
                    filterContext.Result = new RedirectResult(redirectUrl.AbsoluteUri);
                    break;
                case RedirectionStatus.CanNotRedirect:
                    filterContext.Result = new ViewResult { ViewName = "Error" };
                    break;
            }
        }
    }
}
