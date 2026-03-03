@description('Azure region for the OpenAI account')
param location string

@description('Name of the Azure OpenAI account')
param openAiAccountName string

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiAccountName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAiAccountName
    publicNetworkAccess: 'Enabled'
  }
}

resource whisperDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAiAccount
  name: 'whisper-1'
  sku: {
    name: 'Standard'
    capacity: 1
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'whisper'
      version: '001'
    }
  }
}

resource gptDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAiAccount
  name: 'gpt-4.1-mini'
  sku: {
    name: 'Standard'
    capacity: 1
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1-mini'
      version: '2025-04-14'
    }
  }
  dependsOn: [
    whisperDeployment
  ]
}

@description('Endpoint URL for the Azure OpenAI account')
output endpoint string = openAiAccount.properties.endpoint

@description('Name of the Azure OpenAI account')
output accountName string = openAiAccount.name
