@description('Azure region for the Speech Services account')
param location string

@description('Name of the Speech Services account')
param speechAccountName string

resource speechAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: speechAccountName
  location: location
  kind: 'SpeechServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: speechAccountName
    publicNetworkAccess: 'Enabled'
  }
}

@description('Endpoint URL for the Speech Services account')
output endpoint string = speechAccount.properties.endpoint

@description('Name of the Speech Services account')
output accountName string = speechAccount.name
