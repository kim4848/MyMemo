@description('Azure region for the Container Registry')
param location string

@description('Name of the Container Registry (must be globally unique, alphanumeric)')
param registryName string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

@description('Login server URL for the Container Registry')
output loginServer string = registry.properties.loginServer

@description('Name of the Container Registry')
output name string = registry.name
