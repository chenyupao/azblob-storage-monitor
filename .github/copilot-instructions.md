# Copilot Instructions

## Build, test, and validation

The solution targets .NET 10 and uses the isolated-worker Azure Functions v4 model.
Run commands from the repository root:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Run the local Function host from the Function project directory with Azure
Functions Core Tools v4:

```powershell
Set-Location src\AzBlobStorageMonitor.Functions
func start
```

For a focused xUnit run, use the test project and a fully qualified test name
or a `FullyQualifiedName~` filter:

```powershell
dotnet test tests\AzBlobStorageMonitor.Tests\AzBlobStorageMonitor.Tests.csproj `
  --filter "FullyQualifiedName~GrowthTrendEvaluatorTests.ReturnsTrueForThreeConsecutiveDailyIncreases"
```

Before deploying infrastructure, validate the subscription-scope Bicep and
run Checkov:

```powershell
az bicep build --file infra\main.bicep
checkov -d infra --framework bicep
az deployment sub what-if `
  --location eastus2 `
  --template-file infra\main.bicep `
  --parameters infra\main.bicepparam
```

The repository does not define a separate lint script; Bicep compilation and
Checkov are the infrastructure validation steps documented by the project.

## Architecture

- `src\AzBlobStorageMonitor.Functions` is a timer-triggered .NET isolated
  Function. `CheckContainerGrowthFunction` runs on the NCRONTAB value in
  `GrowthMonitorSchedule` and delegates all monitoring behavior to
  `ContainerGrowthMonitor`.
- `AzureMonitorContainerSizeReader` queries the monitored storage account's
  `ContainerUsedSize` metric over the configured lookback window. It filters by
  container, takes the latest average for every blob type/access-tier time
  series, and sums those values into the current container size.
- `TableMonitorHistoryStore` persists state in Azure Table Storage. It stores
  one replaceable sample row per monitor and UTC date plus one `state` row for
  whether the current growth streak has already alerted. The monitor name is
  the partition key, so it must remain stable for the life of a monitor.
- `GrowthTrendEvaluator` requires exactly `GrowthWindowDays + 1` consecutive
  calendar-day samples and a strictly positive, optionally minimum-sized
  increase between every pair. A missing day or non-growing day fails the
  condition.
- `ContainerGrowthMonitor` saves the current sample before evaluating the
  window. It sends a `GrowthAlert` through `LogicAppNotificationSender` only
  when the streak is valid and inactive, then marks the alert active. A
  non-growing run clears the active state, allowing a later streak to alert
  again. State is marked active only after notification succeeds.
- `Program` wires production adapters through dependency injection and uses
  `DefaultAzureCredential` in the `Development` environment and
  `ManagedIdentityCredential` otherwise. Do not add storage keys or other
  credentials to settings.
- `infra\main.bicep` is a subscription-scope deployment that creates the
  monitoring resource group, private networking and state storage, monitoring,
  Logic App, Flex Consumption Function App, managed identity, and RBAC. It
  references the existing monitored storage account and assigns Monitoring
  Reader there; it does not recreate that account.

## Repository-specific conventions

- Configuration is loaded manually by `MonitorOptions.Load` from the
  `GrowthMonitor` section. Required values must fail fast with
  `InvalidOperationException`; numeric settings are validated as positive or
  non-negative. Keep `GrowthWindowDays` compatible with the legacy
  `ConsecutiveGrowthDays` alias.
- Use UTC for monitoring dates and preserve `DateOnly` values as
  `yyyy-MM-dd`. Table sample row keys use `sample|yyyyMMdd`; the fixed `state`
  row and monitor-name partition key are part of the persistence contract.
- Keep Azure, Table Storage, HTTP notification, and time access behind the
  existing interfaces or injectable collaborators (`IContainerSizeReader`,
  `IMonitorHistoryStore`, `INotificationSender`, and `TimeProvider`) so domain
  behavior remains deterministic and unit-testable.
- Treat a growth streak as `GrowthWindowDays` increases, which means
  `GrowthWindowDays + 1` daily measurements. Do not change this to a sample
  count without updating the evaluator, monitor, configuration documentation,
  and tests together.
- Alert payloads are shaped for the Logic App webhook and include subscribers,
  subject, summary, total growth, percentage, and ISO-formatted samples.
  `LogicAppNotificationSender` calls `EnsureSuccessStatusCode`; preserve
  notification failures so alert state is not incorrectly marked active.
- Tests use xUnit and local fakes, including a fixed `TimeProvider`. Prefer
  testing orchestration through `ContainerGrowthMonitor` and pure streak rules
  through `GrowthTrendEvaluator`, rather than requiring Azure services.
- Keep infrastructure parameters and application settings aligned:
  `growthWindowDays`, `minimumDailyGrowthBytes`, the timer schedule, monitor
  identity, Table endpoint/name, Logic App callback, and subscriber list are
  wired from `infra\main.bicep` into Function App settings.
- The generated Logic App intentionally contains a
  `Configure_email_action` Compose placeholder. It must be replaced with an
  authenticated email connector action before production notifications are
  considered complete.
