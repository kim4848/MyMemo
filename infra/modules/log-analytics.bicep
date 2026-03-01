@description('Azure region for the Log Analytics Workspace')
param location string

@description('Name of the Log Analytics Workspace')
param workspaceName string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

@description('Resource ID of the Log Analytics Workspace')
output workspaceId string = workspace.id
