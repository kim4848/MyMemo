@description('Azure region for the Container Apps Environment')
param location string

@description('Name of the Container Apps Environment')
param environmentName string

@description('Resource ID of the Log Analytics Workspace')
param logAnalyticsWorkspaceId string

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
  }
}

@description('Resource ID of the Container Apps Environment')
output environmentId string = environment.id
