param location string
param name string
param tags object
param workspaceResourceId string

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    DisableIpMasking: false
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    RetentionInDays: 30
    WorkspaceResourceId: workspaceResourceId
  }
}

output connectionString string = applicationInsights.properties.ConnectionString
output id string = applicationInsights.id
output name string = applicationInsights.name
