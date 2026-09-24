targetScope = 'subscription'

@description('Azure region for the new monitoring resources.')
param location string

@description('Short workload name used in resource names.')
@minLength(3)
@maxLength(20)
param workloadName string = 'blobmon'

@description('Environment name such as dev, test, or prod.')
@minLength(2)
@maxLength(8)
param environmentName string = 'prod'

@description('Resource group created for the monitoring workload.')
param workloadResourceGroupName string

@description('Subscription containing the existing storage account to monitor.')
param monitoredStorageSubscriptionId string

@description('Resource group containing the existing storage account to monitor.')
param monitoredStorageResourceGroupName string

@description('Name of the existing storage account to monitor.')
param monitoredStorageAccountName string

@description('Blob container whose capacity is monitored.')
param monitoredContainerName string

@description('Email subscribers forwarded to the Logic App payload.')
param subscribers array

@description('Number of consecutive daily increases required before notification.')
@minValue(1)
param growthWindowDays int = 3

@description('Minimum number of bytes the container must grow each day.')
@minValue(0)
param minimumDailyGrowthBytes int = 0

@description('Container measurement implementation. BlobListing avoids preview metrics by enumerating live blobs.')
@allowed([
  'AzureMonitorMetrics'
  'BlobListing'
])
param measurementSource string = 'AzureMonitorMetrics'

@description('UTC NCRONTAB schedule used by the timer trigger.')
param growthMonitorSchedule string = '0 15 0 * * *'

@description('Maximum Flex Consumption instance count.')
@minValue(40)
@maxValue(1000)
param maximumInstanceCount int = 40

@description('Memory allocated to each Flex Consumption instance.')
@allowed([
  2048
  4096
])
param instanceMemoryMB int = 2048

@description('Optional principal ID (user, group, or service principal) granted read-only access to the state storage account so it can be browsed in the Azure Portal or Storage Explorer.')
param resourceOwnerPrincipalId string = ''

@description('Tags applied to new resources.')
param tags object = {}

var resourceToken = take(toLower(uniqueString(subscription().id, workloadResourceGroupName, location)), 8)
var functionAppName = take('func-${workloadName}-${environmentName}-${resourceToken}', 60)
var planName = take('plan-${workloadName}-${environmentName}-${resourceToken}', 60)
var identityName = take('id-${workloadName}-${environmentName}-${resourceToken}', 128)
var storageAccountName = take('st${toLower(uniqueString(subscription().id, workloadResourceGroupName))}', 24)
var workspaceName = take('log-${workloadName}-${environmentName}-${resourceToken}', 63)
var appInsightsName = take('appi-${workloadName}-${environmentName}-${resourceToken}', 260)
var logicAppName = take('logic-${workloadName}-${environmentName}-${resourceToken}', 80)
var deploymentContainerName = 'app-package-${take(resourceToken, 8)}'
var historyTableName = 'BlobGrowthHistory'
var monitorName = '${monitoredStorageAccountName}-${monitoredContainerName}'

resource workloadResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: workloadResourceGroupName
  location: location
  tags: tags
}

module stateStorage './modules/state-storage.bicep' = {
  name: 'state-storage'
  scope: workloadResourceGroup
  params: {
    location: location
    storageAccountName: storageAccountName
    deploymentContainerName: deploymentContainerName
    historyTableName: historyTableName
    tags: tags
  }
}

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: workloadResourceGroup
  params: {
    location: location
    workspaceName: workspaceName
    appInsightsName: appInsightsName
    tags: tags
  }
}

module notification './modules/notification-workflow.bicep' = {
  name: 'notification-workflow'
  scope: workloadResourceGroup
  params: {
    location: location
    logicAppName: logicAppName
    tags: tags
  }
}

module functionApp './modules/function-app.bicep' = {
  name: 'function-app'
  scope: workloadResourceGroup
  params: {
    location: location
    functionAppName: functionAppName
    planName: planName
    identityName: identityName
    storageAccountName: storageAccountName
    deploymentContainerName: deploymentContainerName
    historyTableName: historyTableName
    appInsightsName: appInsightsName
    logicAppName: logicAppName
    monitoredStorageSubscriptionId: monitoredStorageSubscriptionId
    monitoredStorageResourceGroupName: monitoredStorageResourceGroupName
    monitoredStorageAccountName: monitoredStorageAccountName
    monitoredContainerName: monitoredContainerName
    monitorName: monitorName
    subscribers: subscribers
    growthWindowDays: growthWindowDays
    minimumDailyGrowthBytes: minimumDailyGrowthBytes
    measurementSource: measurementSource
    growthMonitorSchedule: growthMonitorSchedule
    maximumInstanceCount: maximumInstanceCount
    instanceMemoryMB: instanceMemoryMB
    resourceOwnerPrincipalId: resourceOwnerPrincipalId
    tags: tags
  }
  dependsOn: [
    monitoring
    notification
    stateStorage
  ]
}

module monitoredStorageRbac './modules/monitored-storage-rbac.bicep' = {
  name: 'monitored-storage-rbac'
  scope: resourceGroup(monitoredStorageSubscriptionId, monitoredStorageResourceGroupName)
  params: {
    monitoredStorageAccountName: monitoredStorageAccountName
    functionPrincipalId: functionApp.outputs.functionPrincipalId
    measurementSource: measurementSource
  }
}

output resourceGroupName string = workloadResourceGroup.name
output functionAppName string = functionApp.outputs.functionAppName
output stateStorageAccountName string = storageAccountName
output logicAppName string = logicAppName
