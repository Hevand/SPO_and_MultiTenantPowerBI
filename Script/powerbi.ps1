param(
  [string]$WorkspaceId = $null,
  [string]$WorkspaceName="Workspace created by powerbi.ps1 script4",
  [string]$ServicePrincipalObjectId
)
Write-Output "  Running Powershell."
Write-Output "  ########################################################################"
# ===============================================================
# Login to powerbi
# ===============================================================
Write-Output "  ########################################################################"
Write-Output "  Please login with PowerBI Admin permissions"
Write-Output "  ########################################################################"
Connect-PowerBIServiceAccount


# ===============================================================
# Login to powerbi
# ===============================================================
Write-Output "  Check if workspace '$WorkspaceId' already exists..."
if (!$WorkspaceId)
{
  Write-Output "  Workspace ID not set. Creating workspace..."
  $rWorkspace = Invoke-PowerBIRestMethod -Url 'https://api.powerbi.com/v1.0/myorg/groups?workspaceV2=True' -Method Post -Body ([pscustomobject]@{name=$WorkspaceName} | ConvertTo-Json -Depth 2 -Compress)
  $WorkspaceId = ($rWorkspace | ConvertFrom-Json).id

  Write-Output "  Workspace created. New workspace id:" $WorkspaceId  
}


# ===============================================================
# Add service principal
# ===============================================================
Write-Output "  Adding service principal '$ServicePrincipalObjectId' object id as admin of '$WorkspaceId'"
Invoke-PowerBIRestMethod -Url "https://api.powerbi.com/v1.0/myorg/groups/$WorkspaceId/users" -Method Post -Body ([pscustomobject]@{identifier=$ServicePrincipalObjectId;groupUserAccessRight="Admin";principalType="App"} | ConvertTo-Json -Depth 2 -Compress)


# ===============================================================
# Instruct user to add content
# ===============================================================
Write-Output "  ########################################################################"
Write-Output "  Created the workspace and set permissions."
Write-Output "  Use the workspace $WorkspaceId to create and configure content"
Write-Output "  Content must be published before it can be used in PowerBI embedded."
Write-Output "  ########################################################################"
Write-Output "  ########################################################################"