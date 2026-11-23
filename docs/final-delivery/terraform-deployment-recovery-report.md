# Azure Terraform Deployment Recovery and Resumption Report

**Project:** Healthcare Multi-Tenant Microservices Platform
**Target Environment:** `dev`
**Primary Resource Group:** `rg-healthcare-platform-dev` (`eastus`)
**Data Tier Location:** `centralindia`
**Azure Subscription:** `Azure subscription 1` (`634797e9-e825-4199-b9c1-40fc24dc3400`)
**Operator Identity:** `manojkumarjaiswal@devopsmaster1outlook.onmicrosoft.com`
**Recovery Timestamp UTC:** 2026-11-22 19:15:00Z
**Status Standard:** Strictly uses authorized statuses (`Successfully Created`, `Already Existing`, `Pending Deployment`, `Failed`, `Requires Manual Intervention`).

---

## 1. Network Disconnection & Incident Investigation

### 1.1 Incident Analysis
- During the previous execution session, an interruption occurred after approximately 14 minutes of execution.
- During state inspection and live Azure verification, `az resource list --resource-group rg-healthcare-platform-dev` returned `ResourceGroupNotFound`.
- **Finding:** The resource group `rg-healthcare-platform-dev` was completely purged outside of Terraform (via automated cleanup or operator action between sessions).
- As a consequence of Azure ARM's cascading deletion, all 12 previously provisioned child resources (APIM, Key Vault, Container Registry, Service Bus, Log Analytics, Application Insights) no longer exist in the subscription.

### 1.2 State Reconciliation & Lock Check
- **Terraform Lock Status:** Checked and clean; no dangling state locks (`.terraform.tfstate.lock.info` does not exist).
- **State Refresh:** Executing `terraform plan` successfully refreshed state against Azure Resource Manager. Terraform detected the external deletion of the 12 resources and automatically reconciled state without errors.
- **Fresh Plan Generated:** `tfplan-resume` was generated cleanly with 0 destructive actions.

---

## 2. Technical Remediations Incorporated into `tfplan-resume`

Prior to generating `tfplan-resume`, three root causes identified from previous diagnostic logs were remediated in the Terraform code:

1. **AKS Kubernetes Version:**
   - Updated from retired `1.29.2` to **`1.36`** (default GA version in East US) in [modules/aks/variables.tf](file:///d:/DevOps/healthcare-multitenant-platform/infrastructure/terraform/modules/aks/variables.tf).
2. **AKS Node VM Size SKU:**
   - Updated from `Standard_D4s_v5` (restricted in subscription in East US) to **`Standard_D4s_v3`** (4 vCPU, 16 GiB RAM, explicitly allowed and quota-compliant).
3. **Data-Tier Multi-Region Routing (Option B):**
   - Azure SQL Server and databases configured for **`centralindia`**.
   - Cosmos DB account and containers configured for **`centralindia`**.
   - Added `timeouts { create = "60m" }` block to `azurerm_cosmosdb_account` in [modules/cosmosdb/main.tf](file:///d:/DevOps/healthcare-multitenant-platform/infrastructure/terraform/modules/cosmosdb/main.tf) to prevent gRPC timeout during replication.

---

## 3. Azure Resource Verification vs. Terraform State

| Resource Name | Service Type | Location | Status | Notes |
| :--- | :--- | :---: | :---: | :--- |
| `rg-healthcare-platform-dev` | Resource Group | `eastus` | **Pending Deployment** | Deleted outside Terraform; queued for recreation. |
| `acrhealthcareplatformdev` | Container Registry | `eastus` | **Pending Deployment** | Standard SKU, admin disabled. |
| `apim-healthcare-dev-01` | API Management | `eastus` | **Pending Deployment** | Developer_1 SKU, SystemAssigned identity. |
| `kv-hlth-dev-01` | Key Vault | `eastus` | **Pending Deployment** | Standard SKU, RBAC enabled, soft delete & purge protection. |
| `asb-healthcare-platform-dev` | Service Bus Namespace | `eastus` | **Pending Deployment** | Standard SKU, messaging backplane. |
| `member-events` | Service Bus Topic | `eastus` | **Pending Deployment** | For member enrollment events. |
| `claim-events` | Service Bus Topic | `eastus` | **Pending Deployment** | For claims ingestion events. |
| `sub-claims-ingestion-member-enrolled` | Topic Subscription | `eastus` | **Pending Deployment** | With `MemberEnrolledFilter` SQL filter rule. |
| `sub-adjudication-claim-received` | Topic Subscription | `eastus` | **Pending Deployment** | For claims adjudication worker. |
| `law-healthcare-platform-dev` | Log Analytics | `eastus` | **Pending Deployment** | Workspace PerGB2018, 30 days retention. |
| `appi-healthcare-platform-dev` | Application Insights | `eastus` | **Pending Deployment** | APM component linked to Log Analytics. |
| `aks-healthcare-platform-dev` | Kubernetes Service | `eastus` | **Pending Deployment** | K8s 1.36, 2x `Standard_D4s_v3` nodes, Azure CNI, Workload Identity. |
| `aks_acr_pull` | RBAC Role Assignment | Azure RBAC | **Pending Deployment** | Scoped `AcrPull` on ACR for AKS Kubelet identity. |
| `sql-healthcare-platform-dev` | Azure SQL Server | `centralindia` | **Pending Deployment** | v12.0, TLS 1.2 minimum. |
| `AllowAzureServices` | SQL Firewall Rule | `centralindia` | **Pending Deployment** | `0.0.0.0` - `0.0.0.0` internal Azure routing. |
| `tenant-a-db` | Dedicated SQL DB | `centralindia` | **Pending Deployment** | S0 (10 DTUs, 50 GB) for Tier 1 Enterprise Tenant A. |
| `shared-payers-db` | Pooled SQL DB | `centralindia` | **Pending Deployment** | S0 (10 DTUs, 50 GB) for Tier 2 Standard Tenants. |
| `cosmos-healthcare-platform-dev` | Cosmos DB Account | `centralindia` | **Pending Deployment** | NoSQL API, Session consistency, analytical storage. |
| `HealthcarePlatform` | Cosmos DB Database | `centralindia` | **Pending Deployment** | Parent database. |
| `claims` | Cosmos DB Container | `centralindia` | **Pending Deployment** | Partition `/tenantId`, Autoscale: 400–4,000 RU/s, TTL: -1. |
| `enrolled-members` | Cosmos DB Container | `centralindia` | **Pending Deployment** | Partition `/tenantId`, Autoscale: max 1,000 RU/s. |
| `idempotency-records` | Cosmos DB Container | `centralindia` | **Pending Deployment** | Partition `/tenantId`, TTL: 7 days. |

---

## 4. Refreshed Plan Summary (`tfplan-resume`)

```text
Plan: 23 to add, 0 to change, 0 to destroy.
Saved the plan to: tfplan-resume
```

- **Operations to Add (+):** 23
- **Operations to Change (~):** 0
- **Operations to Destroy (-):** 0
- **Destructive Changes:** None. Clean, non-destructive deployment.

---

## 5. Cost Considerations (Dev Environment)

| Resource | Sizing / SKU | Estimated Monthly Spend |
| :--- | :--- | :--- |
| **AKS Cluster Compute** | 2x `Standard_D4s_v3` (4 vCPU, 16 GiB RAM) | ~$280.00 / month |
| **API Management** | Developer_1 SKU | ~$50.00 / month |
| **Azure SQL Databases** | 2x S0 Databases (10 DTUs each) | ~$30.00 / month |
| **Cosmos DB** | Autoscale (400–4,000 RU/s baseline) | ~$24.00 / month |
| **Container Registry** | Standard SKU | ~$20.00 / month |
| **Service Bus** | Standard SKU | ~$10.00 / month |
| **AKS Load Balancer & Public IP** | Standard SKU | ~$18.00 / month |
| **Log Analytics & App Insights** | PerGB2018 (~5–10 GB ingestion) | ~$15.00 / month |
| **Key Vault** | Standard SKU | < $1.00 / month |
| **Total Estimated Spend** | | **~$448.00 USD / Month** |

---

## 6. Strict Approval Gate & Operator Resumption Action

> [!WARNING]
> Applying `tfplan-resume` will provision the complete 23-resource architecture across `eastus` and `centralindia`.
> No resources will be modified or destroyed (`0 to change, 0 to destroy`).

Per instructions, `terraform apply` has **not** been executed automatically.

To approve and resume deployment:

```powershell
cd infrastructure/terraform/environments/dev
terraform apply tfplan-resume
```
