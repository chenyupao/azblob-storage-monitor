using './main.bicep'

param location = 'southeastasia'
param workloadName = 'blobmon'
param environmentName = 'prod'
param monitoredStorageSubscriptionId = '<subscription-id>'
param monitoredStorageResourceGroupName = '<storage-resource-group>'
param monitoredStorageAccountName = '<storage-account-name>'
param monitoredContainerName = '<container-name>'
param subscribers = [
  'storage-operations@example.com'
]
param growthWindowDays = 3
param minimumDailyGrowthBytes = 0
// controls which IContainerSizeReader implementation the Function uses: 'AzureMonitorMetrics' or 'BlobListing'
param measurementSource = 'BlobListing'
// grants this principal (user/group/service principal object ID) read access to the state storage account for Azure Portal / Storage Explorer browsing
param resourceOwnerPrincipalId = ''
param tags = {
  workload: 'azblob-storage-monitor'
  environment: 'prod'
  managedBy: 'bicep'
}
