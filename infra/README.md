# Azure deployment

This Bicep baseline creates a low-volume demonstration environment in commercial Azure:

- Linux App Service B1 with one instance
- ASP.NET Core API and compiled React SPA on one origin
- System-assigned managed identity
- Log Analytics and workspace-based Application Insights
- Key Vault with RBAC authorization
- HTTPS only, TLS 1.2 minimum, FTPS disabled, and `/health` monitoring

It does not add persistent case storage, ingestion services, private networking, or Microsoft Entra application authentication. Those controls are required before an agency pilot or production use.

## Prerequisites

- Azure CLI with an authenticated subscription
- Permission to create resource groups, App Service, Monitor, Key Vault, and role assignments
- .NET SDK 10, Node.js 24, and npm for application publishing

Set your subscription and resolve the current user's Microsoft Entra object ID:

```powershell
$subscriptionId = az account show --query id -o tsv
$deployerObjectId = az ad signed-in-user show --query id -o tsv
```

## Validate

```powershell
az bicep build --file infra/main.bicep

az deployment sub validate `
  --name mortality-dev-50f1 `
  --subscription $subscriptionId `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.json `
  --parameters deployerObjectId=$deployerObjectId
```

Review the change set before deployment:

```powershell
az deployment sub what-if `
  --name mortality-dev-50f1 `
  --subscription $subscriptionId `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.json `
  --parameters deployerObjectId=$deployerObjectId
```

## Deploy infrastructure

```powershell
az deployment sub create `
  --name mortality-dev-50f1 `
  --subscription $subscriptionId `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.json `
  --parameters deployerObjectId=$deployerObjectId
```

## Publish the application

Build a single deployment artifact containing the API and SPA:

```powershell
dotnet publish src/MortalityReview.Api/MortalityReview.Api.csproj `
  --configuration Release `
  --output .artifacts/publish

Compress-Archive `
  -Path .artifacts/publish/* `
  -DestinationPath .artifacts/mortality-review.zip `
  -Force

az webapp deploy `
  --resource-group rg-mortality-dev-50f1 `
  --name app-mortality-dev-50f1 `
  --src-path .artifacts/mortality-review.zip `
  --type zip
```

After publishing, disable SCM basic authentication in the Azure portal or by updating the `scm` publishing credential policy to `allow: false`. Confirm the application responds:

```powershell
Invoke-RestMethod https://app-mortality-dev-50f1.azurewebsites.net/health
```

## Production evolution

Before using real records, add Microsoft Entra ID authentication and app roles, Azure SQL for cases and audit history, Blob Storage for governed evidence, Service Bus and Functions for ingestion, private endpoints, backup and recovery objectives, retention policies, and agency security/privacy approvals.

## Cleanup

Review the resource group before deleting the demonstration environment:

```powershell
az resource list --resource-group rg-mortality-dev-50f1 --output table
az group delete --name rg-mortality-dev-50f1
```