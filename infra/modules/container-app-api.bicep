@description('Azure region for the Container App')
param location string

@description('Name of the API Container App')
param appName string

@description('Resource ID of the Container Apps Environment')
param environmentId string

@description('Container Registry login server')
param registryLoginServer string

@description('Container Registry name')
param registryName string

@description('Docker image tag for the API')
param imageTag string = 'latest'

@description('Azure Storage Account name')
param storageAccountName string

@description('SQL Database connection string')
@secure()
param sqlConnectionString string

@description('Clerk secret key')
@secure()
param clerkSecretKey string

@description('Clerk publishable key')
param clerkPublishableKey string

@description('Clerk domain (e.g. fun-terrapin-71.clerk.accounts.dev or your-app.clerk.accounts.com)')
param clerkDomain string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: registryLoginServer
          username: registry.listCredentials().username
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: registry.listCredentials().passwords[0].value
        }
        {
          name: 'storage-connection-string'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
        {
          name: 'clerk-secret-key'
          value: clerkSecretKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: imageTag == 'latest' ? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' : '${registryLoginServer}/mymemo-api:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'AzureBlob__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'StorageQueue__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'ConnectionStrings__SqlDatabase', secretRef: 'sql-connection-string' }
            { name: 'Clerk__SecretKey', secretRef: 'clerk-secret-key' }
            { name: 'Clerk__PublishableKey', value: clerkPublishableKey }
            { name: 'Clerk__Domain', value: clerkDomain }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

@description('FQDN of the API Container App')
output fqdn string = apiApp.properties.configuration.ingress.fqdn
