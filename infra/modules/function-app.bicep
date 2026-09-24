@description('Azure region for Function resources.')
param location string

@description('Function App name.')
param functionAppName string

@description('Flex Consumption plan name.')
param planName string

@description('User-assigned managed identity name.')
param identityName string

@description('State and deployment storage account name.')
param storageAccountName string

@description('Deployment package container name.')
param deploymentContainerName string

@description('History table name.')
param historyTableName string

@description('Application Insights component name.')
param appInsightsName string

@description('Logic App workflow name.')
param logicAppName string

@description('Subscription containing the monitored storage account.')
param monitoredStorageSubscriptionId string

@description('Resource group containing the monitored storage account.')
param monitoredStorageResourceGroupName string

@description('Existing monitored storage account name.')
param monitoredStorageAccountName string

@description('Blob container whose capacity is monitored.')
param monitoredContainerName string

@description('Stable monitor key used as the Table Storage partition key.')
param monitorName string

@description('Email subscribers included in the Logic App payload.')
param subscribers array

@description('Number of consecutive daily increases required.')
param growthWindowDays int

@description('Minimum daily growth in bytes.')
param minimumDailyGrowthBytes int

@description('Container measurement implementation.')
@allowed([
  'AzureMonitorMetrics'
  'BlobListing'
])
param measurementSource string

@description('UTC NCRONTAB timer schedule.')
param growthMonitorSchedule string

@description('Maximum Flex Consumption instance count.')
param maximumInstanceCount int

@description('Memory per Flex Consumption instance.')
param instanceMemoryMB int

@description('Resource tags.')
param tags object

var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource logicApp 'Microsoft.Logic/workflows@2019-05-01' existing = {
  name: logicAppName
}

resource functionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

resource blobOwnerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionIdentity.id, storageBlobDataOwnerRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource blobContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionIdentity.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource queueContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionIdentity.id, storageQueueDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource tableContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionIdentity.id, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource metricsPublisherRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, functionIdentity.id, monitoringMetricsPublisherRoleId)
  scope: applicationInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource flexPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-11-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${functionIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: flexPlan.id
    httpsOnly: true
    clientCertEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: functionIdentity.id
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
  }
  dependsOn: [
    blobOwnerRole
    blobContributorRole
    queueContributorRole
    tableContributorRole
    metricsPublisherRole
  ]
}

resource appSettings 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: {
    AzureWebJobsStorage__accountName: storageAccount.name
    AzureWebJobsStorage__credential: 'managedidentity'
    AzureWebJobsStorage__clientId: functionIdentity.properties.clientId
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.properties.ConnectionString
    APPLICATIONINSIGHTS_AUTHENTICATION_STRING: 'ClientId=${functionIdentity.properties.clientId};Authorization=AAD'
    GrowthMonitorSchedule: growthMonitorSchedule
    GrowthMonitor__MonitorName: monitorName
    GrowthMonitor__StorageAccountResourceId: resourceId(monitoredStorageSubscriptionId, monitoredStorageResourceGroupName, 'Microsoft.Storage/storageAccounts', monitoredStorageAccountName)
    GrowthMonitor__ContainerName: monitoredContainerName
    GrowthMonitor__MeasurementSource: measurementSource
    GrowthMonitor__BlobServiceUri: 'https://${monitoredStorageAccountName}.blob.${environment().suffixes.storage}'
    GrowthMonitor__HistoryTableServiceUri: storageAccount.properties.primaryEndpoints.table
    GrowthMonitor__HistoryTableName: historyTableName
    GrowthMonitor__LogicAppWebhookUrl: listCallbackUrl('${logicApp.id}/triggers/manual', '2019-05-01').value
    GrowthMonitor__GrowthWindowDays: string(growthWindowDays)
    GrowthMonitor__MinimumDailyGrowthBytes: string(minimumDailyGrowthBytes)
    GrowthMonitor__MetricLookbackHours: '24'
    GrowthMonitor__SubscribersCsv: join(subscribers, ',')
  }
}

output functionAppName string = functionApp.name
output functionPrincipalId string = functionIdentity.properties.principalId
