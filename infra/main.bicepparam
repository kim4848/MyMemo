using './main.bicep'

param environmentName = 'mymemo'

param apiImageTag = 'latest'
param workerImageTag = 'latest'

param clerkPublishableKey = ''

// Secure parameters — pass at deploy time via CLI:
//   az deployment group create \
//     --resource-group mymemo-rg \
//     --template-file infra/main.bicep \
//     --parameters infra/main.bicepparam \
//     --parameters tursoUrl='<value>' tursoAuthToken='<value>' clerkSecretKey='<value>'
