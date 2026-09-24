# Azure Blob Storage Growth Monitor

A .NET 10 isolated-worker Azure Function that checks one Blob Storage
container daily and invokes a Logic App webhook when the container grows for
a configurable number of consecutive calendar days.

## Architecture

```mermaid
flowchart LR
    A[Blob Storage container] -->|advanced platform metrics| B[Azure Monitor\nContainerUsedSize]
    C[Timer-triggered Azure Function] -->|queries latest metric values| B
    C -->|stores daily samples| D[Azure Table Storage\nState + history]
    C -->|evaluates consecutive growth| E[Growth trend logic]
    E -->|alert payload| F[Logic App HTTP trigger]
    F -->|sends email| G[Outlook / SMTP / ACS email action]
    H[Subscribers] --> G

    classDef azure fill:#0078D4,color:#fff,stroke:#005A9E;
    classDef app fill:#50E6FF,color:#000,stroke:#0078D4;
    classDef data fill:#D0F0FD,color:#000,stroke:#6CB4EE;
    classDef action fill:#E1F5FE,color:#000,stroke:#81D4FA;

    class A,B azure;
    class C,E app;
    class D data;
    class F,G,H action;
```

1. A timer-triggered Function queries Azure Monitor's `ContainerUsedSize`
   metric for one container.
2. The Function sums the latest metric values across blob type and access tier
   series.
3. One idempotent sample per UTC date is stored in Azure Table Storage.
4. The Function evaluates consecutive calendar-day increases.
5. A Logic App webhook receives an alert payload and sends email to the
   configured subscribers.
6. Alert state prevents duplicate email while the same growth streak
   continues. A non-growing day resets the state.

`ContainerUsedSize` is part of Azure Blob Storage advanced platform metrics,
which is currently in preview.

## Prerequisites

- .NET 10 SDK
- Azure Functions Core Tools v4 for local execution
- Azure CLI for enabling storage metrics and deploying Azure infrastructure
- Azurite for local Function host and Table Storage
- A Blob Storage account with advanced platform metrics enabled
- A state storage account with Table service
- A Logic App with an HTTP request trigger and an email action

.NET 10 is supported by Azure Functions runtime v4 in the isolated worker
model through November 14, 2028. It isn't supported on the legacy Linux
Consumption plan; use Flex Consumption, Premium, Dedicated, or another
supported hosting option.

## Enable the container metric

```powershell
az storage advanced-platform-metric create `
  --resource-group <resource-group> `
  --account-name <storage-account> `
  --enabled `
  --rule-config-filter-type ContainerListFilter `
  --rule-config-filter-values <container-name>
```

Metric data can take up to six hours to appear after the rule changes.

## Azure permissions

Enable a system-assigned managed identity on the Function App and grant it:

- **Monitoring Reader** on the monitored storage account.
- **Storage Table Data Contributor** on the state storage account.

The code uses `ManagedIdentityCredential` in Azure and
`DefaultAzureCredential` during local development. Do not store storage keys
or credentials in settings.

## Configuration

Copy `src\AzBlobStorageMonitor.Functions\local.settings.sample.json` to
`local.settings.json` and replace the placeholders.

| Setting | Meaning |
|---|---|
| `GrowthMonitorSchedule` | NCrontab timer schedule; the sample runs daily at 00:15 UTC |
| `GrowthMonitor__MonitorName` | Stable unique key for this monitored container |
| `GrowthMonitor__StorageAccountResourceId` | Full Azure resource ID of the monitored account |
| `GrowthMonitor__ContainerName` | Container selected in the metric filter |
| `GrowthMonitor__HistoryTableServiceUri` | Managed-identity Table service endpoint |
| `GrowthMonitor__HistoryTableName` | Table used for samples and alert state |
| `GrowthMonitor__LogicAppWebhookUrl` | HTTP-trigger URL for the email Logic App |
| `GrowthMonitor__GrowthWindowDays` | Number of consecutive daily increases required; default `3` |
| `GrowthMonitor__MinimumDailyGrowthBytes` | Minimum increase required each day; default `0` |
| `GrowthMonitor__MetricLookbackHours` | Metric query lookback; default `24` |
| `GrowthMonitor__Subscribers__N` | One email address per numbered setting |
| `GrowthMonitor__SubscribersCsv` | Optional comma-separated subscriber list, used by the Bicep deployment |

Three growth days require four consecutive daily measurements. For example,
100 GB, 110 GB, 120 GB, and 125 GB produce three daily increases.

Set `GrowthMonitor__GrowthWindowDays` to any positive integer. For example,
`5` requires six consecutive daily samples that contain five day-to-day
increases. The former `GrowthMonitor__ConsecutiveGrowthDays` setting remains
accepted as a backward-compatible alias.

## History storage and retention

The alert state uses one fixed Table Storage row per monitor and is replaced
on each update, so it does not grow over time. Daily size history uses one row
per monitor per UTC date. Repeated runs on the same date replace that day's
row, but each new date adds a row.

The current implementation does not delete old samples, so history grows by
approximately 365 rows per monitored container each year. Azure Table Storage
does not provide automatic per-row TTL. If long-term history is not required,
add scheduled cleanup or a retention setting that preserves at least
`GrowthWindowDays + 1` samples.

## Logic App request body

The Function posts JSON shaped like:

```json
{
  "monitorName": "meeting-record-monitor",
  "storageAccountResourceId": "/subscriptions/.../storageAccounts/example",
  "containerName": "archive",
  "subscribers": ["storage-operations@example.com"],
  "subject": "Blob container growth alert: archive",
  "summary": "Container grew for 3 consecutive days.",
  "totalGrowthBytes": 26843545600,
  "totalGrowthPercent": 25.0,
  "samples": [
    { "date": "2026-09-18", "sizeBytes": 107374182400 }
  ]
}
```

Use the `subscribers`, `subject`, and `summary` properties in an Outlook,
SMTP, or Azure Communication Services email action.

Treat the Logic App webhook URL as a secret. For an Azure deployment, store it
in Key Vault and use a Key Vault reference in the Function App setting.

## Build and test

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

To run locally from the Function project:

```powershell
func start
```

## Deploy Azure infrastructure with Bicep

The commands in this section use the Azure CLI (`az`). Sign in to the target
tenant and select the subscription before running them:

```powershell
az login
az account set --subscription <subscription-id-or-name>
```

The `infra` directory contains a subscription-scope Bicep deployment for:

- A .NET 10 Flex Consumption Function App.
- A user-assigned managed identity and least-privilege RBAC assignments.
- A private VNet integration subnet dedicated to Flex Consumption.
- A separate private endpoint subnet.
- A network-restricted StorageV2 account with Blob, Queue, and Table private
  endpoints and private DNS.
- Log Analytics and workspace-based Application Insights.
- A Consumption Logic App with an HTTP request trigger.
- Monitoring Reader access on the existing storage account being monitored.

Update the placeholder values in `infra\main.bicepparam`, then validate:

```powershell
az bicep build --file infra\main.bicep
checkov -d infra --framework bicep
az deployment sub what-if `
  --location eastus2 `
  --template-file infra\main.bicep `
  --parameters infra\main.bicepparam
```

Deploy after reviewing the what-if output:

```powershell
az deployment sub create `
  --name azblob-storage-monitor `
  --location eastus2 `
  --template-file infra\main.bicep `
  --parameters infra\main.bicepparam
```

The deployment is additive: it references the monitored storage account and
does not recreate it. The deployment principal must be allowed to create a
role assignment on that account.

The generated Logic App intentionally contains a `Configure_email_action`
Compose placeholder. Replace it with an authenticated Outlook, SMTP, or Azure
Communication Services email action before relying on notifications. Until
then, the workflow accepts alerts but does not send email.

The Function and its storage dependencies use private networking. Application
Insights ingestion and the Consumption Logic App request trigger remain public
Azure service endpoints. Restricting Azure Monitor requires Azure Monitor
Private Link Scope; private Logic Apps ingress requires a different Logic Apps
hosting design.

## References

- [Advanced platform metrics for Azure Blob Storage](https://learn.microsoft.com/azure/storage/blobs/blob-storage-advanced-platform-metrics)
- [Azure Monitor Query client library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/monitor.query-readme)
- [Azure Functions .NET isolated worker](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
