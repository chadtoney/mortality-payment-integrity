targetScope = 'subscription'

@description('Stable environment identifier from the approved deployment plan.')
param environmentName string

@description('Azure region for every resource in this demonstration environment.')
param location string

@description('App Onboard session identifier used for traceability.')
param sessionId string

@description('Identity that initiated the scaffold.')
param deployedBy string

@description('UTC timestamp when the scaffold parameters were generated.')
param createdAt string

@description('Microsoft Entra object ID that receives Key Vault Secrets Officer.')
param deployerObjectId string

var resourceGroupName = 'rg-mortality-dev-50f1'
var appServicePlanName = 'asp-mortality-dev-50f1'
var appServiceName = 'app-mortality-dev-50f1'
var logAnalyticsName = 'log-mortality-dev-50f1'
var applicationInsightsName = 'appi-mortality-dev-50f1'
var keyVaultName = 'kv-mortality-dev-50f1'
var tags = {
  environment: environmentName
  project: 'mortality-payment-integrity'
  'session-id': sessionId
  'deployed-by': deployedBy
  'created-at': createdAt
  'app-onboard-skill': 'true'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  scope: resourceGroup
  params: {
    location: location
    name: logAnalyticsName
    tags: tags
  }
}

module applicationInsights 'modules/application-insights.bicep' = {
  name: 'application-insights'
  scope: resourceGroup
  params: {
    location: location
    name: applicationInsightsName
    tags: tags
    workspaceResourceId: logAnalytics.outputs.id
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  scope: resourceGroup
  params: {
    location: location
    name: keyVaultName
    tags: tags
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'app-service'
  scope: resourceGroup
  params: {
    location: location
    appName: appServiceName
    planName: appServicePlanName
    tags: tags
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    keyVaultUri: keyVault.outputs.vaultUri
  }
}

module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'role-assignments'
  scope: resourceGroup
  params: {
    appPrincipalId: appService.outputs.principalId
    deployerObjectId: deployerObjectId
    keyVaultName: keyVault.outputs.name
  }
}

output resourceGroupName string = resourceGroup.name
output appServiceName string = appService.outputs.name
output appServiceUrl string = 'https://${appService.outputs.defaultHostName}'
output keyVaultName string = keyVault.outputs.name
