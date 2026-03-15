@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Azure region for the OpenAI account')
param openAiLocation string = 'swedencentral'

@description('Environment name used as prefix for resource names')
param environmentName string

@description('Docker image tag for the API service')
param apiImageTag string = 'latest'

@description('Docker image tag for the Worker service')
param workerImageTag string = 'latest'

@description('SQL Server admin password')
@secure()
param sqlAdminPassword string

@description('Clerk secret key')
@secure()
param clerkSecretKey string

@description('Clerk publishable key')
param clerkPublishableKey string

@description('Clerk domain (e.g. fun-terrapin-71.clerk.accounts.dev)')
param clerkDomain string

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
    registryName: toLower(replace('${environmentName}cr', '-', ''))
  }
}

// --- Storage Account ---
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: toLower(replace('${environmentName}st', '-', ''))
  }
}

// --- Azure OpenAI ---
module openAi 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    location: openAiLocation
    openAiAccountName: '${environmentName}-openai'
  }
}

// --- Azure Speech Services ---
module speech 'modules/speech.bicep' = {
  name: 'speech'
  params: {
    location: openAiLocation
    speechAccountName: '${environmentName}-speech'
  }
}

// --- SQL Database (Free Serverless) ---
module sqlDatabase 'modules/sql-database.bicep' = {
  name: 'sql-database'
  params: {
    location: location
    serverName: '${environmentName}-sql'
    databaseName: 'mymemo'
    adminPassword: sqlAdminPassword
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
    storageAccountName: storage.outputs.accountName
    sqlConnectionString: sqlDatabase.outputs.connectionString
    clerkSecretKey: clerkSecretKey
    clerkPublishableKey: clerkPublishableKey
    clerkDomain: clerkDomain
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
    storageAccountName: storage.outputs.accountName
    sqlConnectionString: sqlDatabase.outputs.connectionString
    openAiEndpoint: openAi.outputs.endpoint
    openAiAccountName: openAi.outputs.accountName
    speechEndpoint: speech.outputs.endpoint
    speechAccountName: speech.outputs.accountName
  }
}

// --- Outputs ---
@description('FQDN of the API Container App')
output apiFqdn string = apiApp.outputs.fqdn

@description('SQL Server FQDN')
output sqlServerFqdn string = sqlDatabase.outputs.serverFqdn

@description('Container Registry login server')
output registryLoginServer string = containerRegistry.outputs.loginServer

@description('Storage Account name')
output storageAccountName string = storage.outputs.accountName

@description('Azure OpenAI endpoint')
output openAiEndpoint string = openAi.outputs.endpoint

@description('Azure Speech Services endpoint')
output speechEndpoint string = speech.outputs.endpoint
