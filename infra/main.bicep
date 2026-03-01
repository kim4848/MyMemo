@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment name used as prefix for resource names')
param environmentName string

@description('Docker image tag for the API service')
param apiImageTag string = 'latest'

@description('Docker image tag for the Worker service')
param workerImageTag string = 'latest'

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

// --- Log Analytics ---
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  params: {
    location: location
    workspaceName: '${environmentName}-logs'
  }
}

// --- Container Registry ---
module containerRegistry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: {
    location: location
    registryName: replace('${environmentName}cr', '-', '')
  }
}

// --- Storage Account ---
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: replace('${environmentName}st', '-', '')
  }
}

// --- Azure OpenAI ---
module openAi 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    location: location
    openAiAccountName: '${environmentName}-openai'
  }
}

// --- Container Apps Environment ---
module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env'
  params: {
    location: location
    environmentName: '${environmentName}-env'
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// --- API Container App ---
module apiApp 'modules/container-app-api.bicep' = {
  name: 'container-app-api'
  params: {
    location: location
    appName: '${environmentName}-api'
    environmentId: containerAppsEnv.outputs.environmentId
    registryLoginServer: containerRegistry.outputs.loginServer
    registryName: containerRegistry.outputs.name
    imageTag: apiImageTag
    storageConnectionString: storage.outputs.connectionString
    tursoUrl: tursoUrl
    tursoAuthToken: tursoAuthToken
    clerkSecretKey: clerkSecretKey
    clerkPublishableKey: clerkPublishableKey
  }
}

// --- Worker Container App ---
module workerApp 'modules/container-app-worker.bicep' = {
  name: 'container-app-worker'
  params: {
    location: location
    appName: '${environmentName}-worker'
    environmentId: containerAppsEnv.outputs.environmentId
    registryLoginServer: containerRegistry.outputs.loginServer
    registryName: containerRegistry.outputs.name
    imageTag: workerImageTag
    storageConnectionString: storage.outputs.connectionString
    tursoUrl: tursoUrl
    tursoAuthToken: tursoAuthToken
    openAiEndpoint: openAi.outputs.endpoint
    openAiKey: openAi.outputs.key
  }
}

// --- Outputs ---
@description('FQDN of the API Container App')
output apiFqdn string = apiApp.outputs.fqdn

@description('Container Registry login server')
output registryLoginServer string = containerRegistry.outputs.loginServer

@description('Storage Account name')
output storageAccountName string = storage.outputs.accountName

@description('Azure OpenAI endpoint')
output openAiEndpoint string = openAi.outputs.endpoint
