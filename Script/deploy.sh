#!/bin/bash
# ===========================================================================================
# ===========================================================================================
# This script is created in context of a multi-tenant SharePoint Online integration with PowerBI
# The main goal of this integration is to present a PowerBI dashboard in O365 
# In this setup:
# - The SharePoint Online environment is owned by Tenant A
# - The Azure AD tenant associated with the SharePoint Online environment is owned by Tenant A
# - The Data, PowerBI and custom application are owned by Tenant B.

# The following actions need to be performed in the environment of Tenant A:
# - [Admin] Register a custom application (client id and secret)
# - [Admin][optional] Create an app repository
# - [Admin] Publish the custom application
# - [Site Admin] Add app to portal

# This script is used to perform the following in Tenant B:
# - Create a Resource Group
# - Create an App Service Plan
# - Create a Web App
# - Create an Azure AD App registration for web/api-based applications
# - Create a Service Principal for the App Registration
# - Create a Security Group
# - Add the App registration to the Security Group

# - [MANUAL] Configure PowerBI to support service principals.
# - Create a PowerBI App Workspace
# - Add the Service Principal as an Admin to this workspace
# - Create a PowerBI Report
# - Publish the PowerBI Workspace
# - Update the application configuration
# - Deploy the application

# - Configure sharepoint
# - Register custom plugin
# - Deploy to app repository

# Relevant links
# https://docs.microsoft.com/en-us/power-bi/service-create-the-new-workspaces
# https://docs.microsoft.com/en-us/power-bi/developer/embed-service-principal

# https://docs.microsoft.com/en-us/sharepoint/dev/sp-add-ins/get-started-creating-provider-hosted-sharepoint-add-ins
# https://docs.microsoft.com/en-us/sharepoint/dev/sp-add-ins/register-sharepoint-add-ins

# https://github.com/Microsoft/PowerBI-Developer-Samples/tree/master/App%20Owns%20Data/ 

# ===========================================================================================
# ===========================================================================================

# ===========================================================================
# Set variables
# ===========================================================================
#ARM
resourceGroup="SAMPLE-POWERBI-RG"
location="WestEurope"
appServicePlan="$resourceGroup-AppServicePlan"
webApp="$resourceGroup-App"

#PowerBI
workspaceName="$resourceGroup-WS"

# ===========================================================================
# Initialize
# ===========================================================================
az login

# ===========================================================================
# Azure web app - Create resource group, app service plan and web app
# ===========================================================================
rg=$(az group create -n $resourceGroup -l $location)
echo "Created resource group $resourceGroup"

asp=$(az appservice plan create -n $appServicePlan -g $resourceGroup)
echo "Created App Service Plan $appServicePlan"

webapp=$(az webapp create -n $webApp -p $appServicePlan -g $resourceGroup)
echo "Created web application $webApp"

# ===========================================================================
# Azure AD - Register an app and Service Principal
# ===========================================================================
# App registration
app="$(az ad app create --display-name $resourceGroup --native-app false --identifier-uris "$webApp.azurewebsites.net" --reply-urls "https://$webApp.azurewebsites.net" --required-resource-accesses @app_permissions.json )"
appId=$(echo $app | jq '.appId' -r)
echo "Application ID to be used: $appId"

# Service principal
sp="$(az ad sp create --id $appId)"
spId=$(echo $sp | jq '.objectId' -r)
echo "Service Principal Id: $spId"

# Security Group
group="$(az ad group create --display-name "$resourceGroup Service Principals" --mail-nickname notSet)"
groupId=$(echo $group | jq '.objectId' -r)
echo "Group Id: $groupId"

# Security Group membership
az ad group member add --group $groupId --member-id $spId

# ============================================================
# Prepare PowerBI Environment
# ============================================================
echo "[MANUAL STEP] As a tenant-level admin, allow for service principals and add the group just created (under developer settings)"
echo "Link: https://docs.microsoft.com/en-us/power-bi/developer/embed-service-principal"

echo "Configuring PowerBI (using a powershell script):"
echo "  Workspace ID: '$workspaceId'"
echo "  Workspace name: '$workspaceName'"
echo "  Service Principal Object Id: '$spId'"
echo "  We use the SPO and not the group because you cannot use groups (the UI allows it but it does not work(?))"
echo "  Starting...."
/mnt/c/Windows/System32/WindowsPowershell/v1.0/powershell.exe -Command "./powerbi.ps1 -WorkspaceName $workspaceName -ServicePrincipalObjectId $spId"
echo "  Completed."
echo "[MANUAL STEP] Add the report to the powerbi workspace"

# ============================================================
# Update SharePoint Online tenant
# ============================================================

# In a SharePoint environment
# 1. Register the App in SharePoint:
#    https://docs.microsoft.com/en-us/sharepoint/dev/sp-add-ins/register-sharepoint-add-ins
#    Go to https://[tenant name].sharepoint.com/_layouts/15/AppRegNew.aspx
#    Generate the Client ID and Secret and update the application configurationstore (web.comfig) of the webapp
#    Use the URL of the webApp for the redirect values 
# 2. Create an add-in repository:
#    Open the visual studio solution and find the addin project
#    Edit the AppManifest.xml (optional)
#    Edit SampleReport.xml (optional)
#    Publish "addin", select "profile" and provide the Client ID and Client secret of the App Registration
#    Select "Package add in" and provide the URL of the webApp and Client ID of the App Registration and click "Finish"
#   
#    Register now this add-in in SharePoint, see https://collab365.community/publish-app-to-non-developer-site-collection/
#    Go to the admin centre - Sharepoint - Apps
#    Select App Cataloque - Apps for SharePoint
#    Click new and upload the published app
# 3. Create a SharePoint site to host the provider-hosted aspect of this app

# ============================================================
# Publish web application to azure website
# ============================================================
# 1. Update the application configuration with the key's and secrets 
#    The powerbi workspace id and report id can be found in the powerBi web client by navigating to the report and get the ID's from the URL
#    The SPS object ID can be found by going to the app registration and selecting "managed application in local directory" and select properties
#    Deploy the application

# ============================================================
# Configure Azure Analysis Services
# ============================================================
# 1. Add rights for the App registration to connect to AAS as a SP.
#    Go to the App registration - API permissions - Add permissions - API's my organization use
#    Scroll down and click More !!!important step!! and select "Azure Analysis Services" 
#    select "Delegate permissions" - select "Model Read/Write" 
#    Go to the App registration and selecting "managed application in local directory" - permissions - Grant Admin Consent for ...
# 2. Create a role in AAS for RLS 
#    The members of this role should be the Client ID of the App registration in the following format: app:{Client-id}@{tenant-id} 
#    Create a DAX rule for example =Employee[Email] = CUSTOMDATA()
# 3. Take a coffee, stop and start AAS and the PowerBI embedded instance
#    test the PowerBI report in SPS
# 4. Troubelshoot options:
#    Start SMSS and logon to AAS with theaccount app:{Client-id}@{tenant-id} and select "Active Directory Password" as logon method and the app secret as password
#		Fire the following querie to check if the DAX is evaluated: 
#       ROW("USERPRINCIPALNAME", USERPRINCIPALNAME())
#       Evaluate SAMPLE(10,Sales,Sales[Id],1)
#	Remove the RLS rule and check if the PBI reports loads the data, if that is the case the RLS syntax is not correct
#	You can debug the webapp by running it locally and add the redirect url to the app registration. Configure in the webconfig that you start the webapp without sps and in debug mode

