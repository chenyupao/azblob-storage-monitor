@description('Existing storage account name.')
param monitoredStorageAccountName string

@description('Function managed identity principal ID.')
param functionPrincipalId string

@description('Container measurement implementation.')
@allowed([
  'AzureMonitorMetrics'
  'BlobListing'
])
param measurementSource string

var monitoringReaderRoleId = '43d0d8ad-25c7-4714-9337-8ba259a9fe05'
var storageBlobDataReaderRoleId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'

resource monitoredStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: monitoredStorageAccountName
}

resource monitoringReaderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (measurementSource == 'AzureMonitorMetrics') {
  name: guid(monitoredStorageAccount.id, functionPrincipalId, monitoringReaderRoleId)
  scope: monitoredStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringReaderRoleId)
    principalId: functionPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource storageBlobDataReaderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (measurementSource == 'BlobListing') {
  name: guid(monitoredStorageAccount.id, functionPrincipalId, storageBlobDataReaderRoleId)
  scope: monitoredStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataReaderRoleId)
    principalId: functionPrincipalId
    principalType: 'ServicePrincipal'
  }
}
