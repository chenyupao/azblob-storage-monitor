@description('Existing storage account name.')
param monitoredStorageAccountName string

@description('Function managed identity principal ID.')
param functionPrincipalId string

var monitoringReaderRoleId = '43d0d8ad-25c7-4714-9337-8ba259a9fe05'

resource monitoredStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: monitoredStorageAccountName
}

resource monitoringReaderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(monitoredStorageAccount.id, functionPrincipalId, monitoringReaderRoleId)
  scope: monitoredStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringReaderRoleId)
    principalId: functionPrincipalId
    principalType: 'ServicePrincipal'
  }
}
