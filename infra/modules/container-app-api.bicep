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

@description('Azure Storage connection string')
@secure()
param storageConnectionString string

@description('Turso database URL')
@secure()
param tursoUrl string

@description('Turso auth token')
@secure()
param tursoAuthToken string

@description('Clerk secret key')
@secure()
param clerkSecretKey string

@description('Clerk publishable key')
param clerkPublishableKey string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
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
          value: storageConnectionString
        }
        {
          name: 'turso-url'
          value: tursoUrl
        }
        {
          name: 'turso-auth-token'
          value: tursoAuthToken
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
            { name: 'Azure__Storage__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'Turso__Url', secretRef: 'turso-url' }
            { name: 'Turso__AuthToken', secretRef: 'turso-auth-token' }
            { name: 'Clerk__SecretKey', secretRef: 'clerk-secret-key' }
            { name: 'Clerk__PublishableKey', value: clerkPublishableKey }
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
