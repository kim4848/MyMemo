@description('Azure region for the SQL Server')
param location string

@description('Name of the SQL Server')
param serverName string

@description('Name of the SQL Database')
param databaseName string = 'mymemo'

@description('SQL Server admin username')
param adminLogin string = 'mymemoadmin'

@description('SQL Server admin password')
@secure()
param adminPassword string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow Azure services (Container Apps) to connect
resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368 // 32 GB
    autoPauseDelay: 60 // Auto-pause after 60 min of inactivity
    minCapacity: json('0.5')
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'BillOverUsage'
  }
}

@description('Fully qualified domain name of the SQL Server')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('SQL connection string for the database')
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Persist Security Info=False;User ID=${adminLogin};Password=${adminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
