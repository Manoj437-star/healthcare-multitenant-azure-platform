# Azure Terraform Apply & Live Resource Verification Report

**Project:** Healthcare Multi-Tenant Microservices Platform
**Environment:** `dev`
**Target Resource Group:** `rg-healthcare-platform-dev` (Location: `eastus`)
**Azure Subscription:** `Azure subscription 1` (`634797e9-e825-4199-b9c1-40fc24dc3400`)
**Operator Identity:** `manojkumarjaiswal@devopsmaster1outlook.onmicrosoft.com`
**Execution Timestamp UTC:** 2026-11-19 12:35:00Z
**Command Executed:** `terraform apply -no-color tfplan`

---

## 1. Executive Summary

The approved Terraform execution plan (`tfplan`) was executed against the active Azure subscription.

The deployment successfully provisioned the core architectural backplane:
- **Central Management & Observability:** Resource Group, Log Analytics Workspace, Application Insights, Smart Detection Action Group.
- **Enterprise Messaging:** Azure Service Bus Namespace, 2 Topics (`member-events`, `claim-events`), Subscriptions, and SQL Filter Rules.
- **Container Registry & Ingress:** Azure Container Registry (Standard SKU) and Azure API Management Gateway (Developer SKU).
- **Security & Secrets:** Azure Key Vault with Azure RBAC, 90-day soft delete, and purge protection.

Three services encountered Azure platform and regional subscription restrictions in `eastus`:
1. **Azure Kubernetes Service (AKS):** Hardcoded version `1.29.2` is retired in `eastus` (Azure requires version `1.31+`).
2. **Azure Cosmos DB:** Regional capacity constraint in `eastus` on this subscription (`ServiceUnavailable: high demand in East US`).
3. **Azure SQL Database Server:** Regional provisioning restriction for SQL servers in `eastus` on this subscription (`ProvisioningDisabled`).

---

## 2. Terraform Outputs (`terraform output`)

```text
acr_login_server                = "acrhealthcareplatformdev.azurecr.io"
aks_cluster_name                = "aks-healthcare-platform-dev"
apim_gateway_url                = "https://apim-healthcare-dev-01.azure-api.net"
key_vault_uri                   = "https://kv-hlth-dev-01.vault.azure.net/"
resource_group_name             = "rg-healthcare-platform-dev"
servicebus_namespace            = "asb-healthcare-platform-dev"
synapse_serverless_sql_endpoint = "Synapse Disabled"
```

---

## 3. Azure CLI Resource Verification

Live resources in `rg-healthcare-platform-dev` were queried and verified via Azure CLI (`az resource list` and specific CLI providers):

| Resource Name | Azure Resource Type | Provisioning State | Verified Endpoint / Configuration |
| :--- | :--- | :---: | :--- |
| **`rg-healthcare-platform-dev`** | `Microsoft.Resources/resourceGroups` | **Succeeded** | Location: `eastus` |
| **`acrhealthcareplatformdev`** | `Microsoft.ContainerRegistry/registries` | **Succeeded** | Login Server: `acrhealthcareplatformdev.azurecr.io` (Standard SKU, admin disabled) |
| **`apim-healthcare-dev-01`** | `Microsoft.ApiManagement/service` | **Succeeded** | Gateway: `https://apim-healthcare-dev-01.azure-api.net` (Developer SKU) |
| **`kv-hlth-dev-01`** | `Microsoft.KeyVault/vaults` | **Succeeded** | URI: `https://kv-hlth-dev-01.vault.azure.net/` (RBAC enabled, purge protection) |
| **`asb-healthcare-platform-dev`** | `Microsoft.ServiceBus/namespaces` | **Succeeded** | Endpoint: `https://asb-healthcare-platform-dev.servicebus.windows.net:443/` |
| **`member-events`** | `.../servicebus/namespaces/topics` | **Active** | Subscriptions: `sub-claims-ingestion-member-enrolled` |
| **`claim-events`** | `.../servicebus/namespaces/topics` | **Active** | Subscriptions: `sub-adjudication-claim-received` |
| **`law-healthcare-platform-dev`** | `Microsoft.OperationalInsights/workspaces` | **Succeeded** | SKU: `PerGB2018`, Retention: 30 days |
| **`appi-healthcare-platform-dev`** | `Microsoft.Insights/components` | **Succeeded** | App ID: `1c46eb18-573f-4834-83db-b88aafe0e94b` |
| **`Application Insights Smart Detection`** | `microsoft.insights/actiongroups` | **Succeeded** | Automated anomaly detection active |
| **`cosmos-healthcare-platform-dev`** | `Microsoft.DocumentDB/databaseAccounts` | **Failed** | Region capacity restriction in `eastus` (Details below) |
| **`aks-healthcare-platform-dev`** | `Microsoft.ContainerService/managedClusters` | **Not Created** | K8s version `1.29.2` retired (Details below) |
| **`sql-healthcare-platform-dev`** | `Microsoft.Sql/servers` | **Not Created** | Provisioning restricted in `eastus` (Details below) |

---

## 4. Detailed Failure Analysis & Diagnostic Logs

### 4.1 Azure Kubernetes Service (AKS)
- **Error Code:** `400 Bad Request - K8sVersionNotSupported`
- **Azure Diagnostic Response:**
  ```json
  {
    "code": "K8sVersionNotSupported",
    "message": "Managed cluster aks-healthcare-platform-dev is on version 1.29.2 which is not supported in this region. Please use [az aks get-versions] command to get the supported version list in this region."
  }
  ```
- **Root Cause:** Version `1.29.2` was hardcoded in `modules/aks/variables.tf`. Azure regularly deprecates and retires Kubernetes versions. Currently, `eastus` requires versions `1.31` through `1.37`.
- **Blocked Downstream Resources:**
  - `azurerm_role_assignment.aks_acr_pull[0]` (AcrPull role assignment for Kubelet identity).
- **Remediation:** Update `kubernetes_version` in `modules/aks/variables.tf` to `"1.31.13"` or `"1.32.10"` (currently supported patch versions in `eastus`).

### 4.2 Azure Cosmos DB Account
- **Error Code:** `503 ServiceUnavailable`
- **Azure Diagnostic Response:**
  ```json
  {
    "code": "ServiceUnavailable",
    "message": "Sorry, we are currently experiencing high demand in East US region, and cannot fulfill your request at this time Thu, 19 Nov 2026 12:00:20 GMT. To request region access for your subscription, please follow this link https://aka.ms/cosmosdbquota for more details on how to create a region access request."
  }
  ```
- **Root Cause:** Microsoft Azure platform capacity limitation in `eastus` for Cosmos DB accounts under this subscription.
- **Blocked Downstream Resources:**
  - `azurerm_cosmosdb_sql_database.db` (`HealthcarePlatform`)
  - `azurerm_cosmosdb_sql_container.claims`
  - `azurerm_cosmosdb_sql_container.members`
  - `azurerm_cosmosdb_sql_container.idempotency`
- **Remediation:** Request regional quota increase via Azure portal (https://aka.ms/cosmosdbquota) or target an alternative region with available capacity (e.g., `centralindia` or `westus2`).

### 4.3 Azure SQL Database Server
- **Error Code:** `400 Bad Request - ProvisioningDisabled`
- **Azure Diagnostic Response:**
  ```json
  {
    "code": "ProvisioningDisabled",
    "message": "Provisioning is restricted in this region. Please choose a different region. For exceptions to this rule please open a support request with Issue type of 'Service and subscription limits'."
  }
  ```
- **Root Cause:** Azure SQL Server provisioning is disabled for this subscription type in `eastus` (Azure limits SQL provisioning in high-demand regions on specific subscription tiers to preserve capacity for existing enterprise agreements).
- **Blocked Downstream Resources:**
  - `azurerm_mssql_database.db_tenant_a` (`tenant-a-db`)
  - `azurerm_mssql_database.db_shared_payers` (`shared-payers-db`)
  - `azurerm_mssql_firewall_rule.allow_azure_services`
- **Remediation:** Request quota via Azure support request or configure an available subscription region (e.g., `centralindia` where the subscription already has active workloads).

---

## 5. Security & Secret Exposure Audit

- **Secrets Sanitization:** All outputs from `terraform apply`, `az account show`, `az acr show`, `az keyvault show`, and `az servicebus show` have been strictly scrubbed.
- **Zero Raw Secrets:** No passwords, connection strings, JWT tokens, or client secrets have been written to this report or checked into version control.
- **Key Vault Isolation:** Vault `kv-hlth-dev-01` is protected by Azure RBAC (`enable_rbac_authorization = true`), ensuring only authorized identities can read secrets.

---

## 6. Action Plan for Next Phase

To complete the full infrastructure rollout:

1. **Fix AKS Kubernetes Version:**
   In `infrastructure/terraform/modules/aks/variables.tf`, update:
   ```hcl
   variable "kubernetes_version" {
     type    = string
     default = "1.31.13"
   }
   ```
2. **Resolve Regional Quotas (Cosmos DB & Azure SQL):**
   - Option A: Submit Azure quota request tickets via Azure Portal for `eastus`.
   - Option B: Multi-region pairing: Keep APIM, Key Vault, Service Bus, and ACR in `eastus`, and set data service region to an active subscription region (e.g., `centralindia`).
3. **Re-run Targeted Plan & Apply:**
   Once adjusted, run `terraform plan` and `terraform apply` to provision the remaining 10 resources.
