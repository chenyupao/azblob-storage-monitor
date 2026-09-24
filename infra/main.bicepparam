using './main.bicep'

param location = 'eastus2'
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
param measurementSource = 'BlobListing'
param tags = {
  workload: 'azblob-storage-monitor'
  environment: 'prod'
  managedBy: 'bicep'
}
