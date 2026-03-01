@description('Azure region for the Container App')
param location string

@description('Name of the Worker Container App')
param appName string

@description('Resource ID of the Container Apps Environment')
param environmentId string

@description('Container Registry login server')
param registryLoginServer string

@description('Container Registry name')
param registryName string

@description('Docker image tag for the Worker')
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

@description('Azure OpenAI endpoint')
param openAiEndpoint string

@description('Azure OpenAI API key')
@secure()
param openAiKey string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
}

resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
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
          name: 'openai-key'
          value: openAiKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: '${registryLoginServer}/mymemo-worker:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'Azure__Storage__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'Turso__Url', secretRef: 'turso-url' }
            { name: 'Turso__AuthToken', secretRef: 'turso-auth-token' }
            { name: 'Azure__OpenAI__Endpoint', value: openAiEndpoint }
            { name: 'Azure__OpenAI__Key', secretRef: 'openai-key' }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 4
        rules: [
          {
            name: 'transcription-queue'
            azureQueue: {
              queueName: 'transcription-jobs'
              queueLength: 1
              auth: [
                {
                  secretRef: 'storage-connection-string'
                  triggerParameter: 'connection'
                }
              ]
            }
          }
          {
            name: 'memo-queue'
            azureQueue: {
              queueName: 'memo-generation'
              queueLength: 1
              auth: [
                {
                  secretRef: 'storage-connection-string'
                  triggerParameter: 'connection'
                }
              ]
            }
          }
        ]
      }
    }
  }
}
