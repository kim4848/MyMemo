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

@description('Azure Storage Account name')
param storageAccountName string

@description('SQL Database connection string')
@secure()
param sqlConnectionString string

@description('Azure OpenAI endpoint')
param openAiEndpoint string

@description('Azure OpenAI account name')
param openAiAccountName string

@description('Azure Speech Services endpoint')
param speechEndpoint string

@description('Azure Speech Services account name')
param speechAccountName string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: openAiAccountName
}

resource speechAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: speechAccountName
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
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
        {
          name: 'openai-key'
          value: openAiAccount.listKeys().key1
        }
        {
          name: 'speech-key'
          value: speechAccount.listKeys().key1
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: imageTag == 'latest' ? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' : '${registryLoginServer}/mymemo-worker:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AzureBlob__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'StorageQueue__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'ConnectionStrings__SqlDatabase', secretRef: 'sql-connection-string' }
            { name: 'AzureOpenAI__Endpoint', value: openAiEndpoint }
            { name: 'AzureOpenAI__ApiKey', secretRef: 'openai-key' }
            { name: 'AzureSpeech__Endpoint', value: speechEndpoint }
            { name: 'AzureSpeech__ApiKey', secretRef: 'speech-key' }
            { name: 'AzureSpeech__Region', value: 'swedencentral' }
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
          {
            name: 'infographic-queue'
            azureQueue: {
              queueName: 'infographic-generation'
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
