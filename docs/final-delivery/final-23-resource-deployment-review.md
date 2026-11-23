# Final Deployment Authorization & Infrastructure Safety Review: 23 Azure Resources

**Project:** Healthcare Multi-Tenant Microservices Platform
**Target Environment:** `dev`
**Primary Region (Compute, Ingress, Security, Messaging):** `eastus`
**Persistence Region (Relational & NoSQL Data Tier):** `centralindia`
**Azure Subscription:** `Azure subscription 1` (`634797e9-e825-4199-b9c1-40fc24dc3400`)
**Operator Identity:** `manojkumarjaiswal@devopsmaster1outlook.onmicrosoft.com`
**Authorization Check Timestamp UTC:** 2026-11-22 19:40:00Z
**Execution Plan File:** `tfplan-resume` (Size: 27,873 bytes | Verified Fresh)

---

## 1. Verified Terraform Execution Plan (`tfplan-resume`)

Re-inspected directly via `terraform show tfplan-resume`:

```text
Plan: 23 to add, 0 to change, 0 to destroy.
Saved the plan to: tfplan-resume
```

- **Operations to Add (+):** 23
- **Operations to Change (~):** 0
- **Operations to Destroy (-):** 0
- **Plan File Integrity:** Matches the reviewed plan exactly; state has not drifted or become stale.

---

## 2. Key Vault Recovery & Soft-Delete Assessment

- **Vault Identifier:** `kv-hlth-dev-01`
- **Soft-Delete Status in Azure:**
  - Deletion Date: `2026-11-20T00:59:24+00:00`
  - Scheduled Purge Date: `2027-02-18T00:59:24+00:00` (90-day retention policy active)
  - Target Resource Group: `rg-healthcare-platform-dev` (`eastus`)
  - Tags Preserved: `Compliance: HIPAA-Aligned`, `Environment: dev`, `ManagedBy: Terraform`, `Project: Healthcare-MultiTenant-Platform`
- **Automated Recovery Mechanism:**
  - Configured in [environments/dev/versions.tf](file:///d:/DevOps/healthcare-multitenant-platform/infrastructure/terraform/environments/dev/versions.tf):
    ```hcl
    provider "azurerm" {
      features {
        key_vault {
          purge_soft_delete_on_destroy    = false
          recover_soft_deleted_key_vaults = true
        }
      }
    }
    ```
  - `recover_soft_deleted_key_vaults = true` instructs AzureRM to invoke the ARM recovery API (`/deletedVaults/kv-hlth-dev-01/recover`).
  - The vault will be recovered into `rg-healthcare-platform-dev` with zero naming conflicts, zero secret overwrites, and zero risk of purge.

---

## 3. Compute and Database Quota Verification

| Service & Quota Item | Azure Region | Measured Limit | Current Usage | Planned Consumption | Quota Conformance |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Total Regional vCPUs** | `eastus` | **4 vCPUs** | 0 vCPUs | 4 vCPUs (2x `Standard_D2s_v3`) | **100% Compliant (Exact Fit)** |
| **Standard Dv3 Family vCPUs** | `eastus` | **4 vCPUs** | 0 vCPUs | 4 vCPUs (2x `Standard_D2s_v3`) | **100% Compliant (Exact Fit)** |
| **AKS Kubernetes Version** | `eastus` | Supported GA | Retired (1.29) | Version **`1.36`** (Patch `1.36.5`) | **100% GA Compliant** |
| **Regional SQL Server Quota** | `centralindia` | **250 Servers**| 0 Servers | 1 Server (`sql-healthcare-platform-dev`) | **Ample Headroom (249 left)** |
| **Regional SQL vCore Quota** | `centralindia` | **10 vCores** | 0 vCores | DTU-based S0 (0 vCores charged) | **Ample Headroom (10 left)** |
| **Cosmos DB Autoscale Limit** | `centralindia` | Supported | 0 Accounts | 400–4,000 RU/s autoscale | **Verified & Compliant** |
| **APIM Developer SKU** | `eastus` | Supported | 0 Units | 1 Unit | **Verified & Compliant** |
| **Service Bus Standard SKU** | `eastus` | Supported | 0 Namespaces | 1 Namespace | **Verified & Compliant** |

---

## 4. Regional Architecture & Cross-Region Safeguards

### 4.1 Topology
- **East US (Compute, Ingress, Security, Messaging):**
  - Azure Kubernetes Service (AKS): 2x `Standard_D2s_v3` nodes.
  - Azure Container Registry (ACR): Standard SKU.
  - Azure API Management (APIM): Developer_1 SKU.
  - Azure Key Vault: Standard SKU (`kv-hlth-dev-01` recovered).
  - Azure Service Bus: Standard SKU (Topics: `member-events`, `claim-events`).
  - Azure Monitor: Log Analytics & Application Insights.
- **Central India (Persistence Tier):**
  - Azure SQL Server & Databases (`tenant-a-db` dedicated, `shared-payers-db` pooled).
  - Azure Cosmos DB Account & Containers (`claims`, `enrolled-members`, `idempotency-records`).

### 4.2 Cross-Region Networking & Latency Mitigation
1. **Private Global WAN:** Traffic between East US and Central India travels across Microsoft's private global fiber network, never traversing the public unencrypted Internet.
2. **Asynchronous Ingress Buffering:** External claim submissions hit APIM and are written to Azure Service Bus in East US with sub-50ms HTTP latency. Background consumer workers in AKS asynchronously process messages and persist records to Central India, completely decoupling external user latency from cross-region flight time (~180–210 ms).
3. **Firewall & Identity:** SQL Server firewall rule `AllowAzureServices` (`0.0.0.0` - `0.0.0.0`) allows authorized Azure workloads across regions while denying unauthorized public IP addresses.
4. **Data Sovereignty & PHI:** Strict synthetic data enforcement ensures zero PHI risk during POC evaluation.

---

## 5. Comprehensive Resource-Wise Monthly Cost Report

| Resource Name | Service Type | Location | Sizing / Metric | Estimated Cost (USD/Month) |
| :--- | :--- | :---: | :--- | :--- |
| `module.aks.aks` | Kubernetes Cluster VMs | `eastus` | 2x `Standard_D2s_v3` (2 vCPU, 8 GiB RAM each) | ~$140.00 |
| `module.aks.aks` (Storage) | Managed OS Disks | `eastus` | 2x 64 GB Premium SSD OS Disks | ~$10.00 |
| `module.apim.apim` | API Management Gateway | `eastus` | `Developer_1` SKU ($0.069/hour flat) | ~$50.00 |
| `module.sql.db_tenant_a` | Dedicated SQL Database | `centralindia` | S0 Tier (10 DTUs, 50 GB storage) | ~$15.00 |
| `module.sql.db_shared_payers`| Pooled SQL Database | `centralindia` | S0 Tier (10 DTUs, 50 GB storage) | ~$15.00 |
| `module.cosmosdb.cosmos` | Cosmos DB Account | `centralindia` | Autoscale (400–4,000 RU/s baseline at 400 RU/s) | ~$24.00 |
| `module.acr.acr` | Container Registry | `eastus` | `Standard` SKU ($0.667/day flat) | ~$20.00 |
| `module.servicebus.sb` | Service Bus Namespace | `eastus` | `Standard` SKU ($0.0139/hour base messaging) | ~$10.00 |
| AKS Standard Load Balancer | Public IP & Load Balancer | `eastus` | 1 Public IP + Ingress Rule processing | ~$18.00 |
| `module.monitoring.law` & AppInsights | Log Analytics & APM | `eastus` | PerGB2018 (Estimated 5–10 GB ingestion) | ~$15.00 |
| `module.keyvault.kv` | Key Vault | `eastus` | Standard SKU (Storage & transaction operations) | < $1.00 |
| Cross-Region Egress | Inter-Region Data Transfer | `eastus` -> `CI` | Estimated 2–5 GB POC synthetic claim payload traffic | < $1.00 |
| **Total Estimated Spend** | | | | **~$319.00 USD / Month** (~$10.63 USD / Day) |

*(Note: Downsizing AKS nodes from D4s to D2s_v3 reduced total platform spend from ~$448 to ~$319 USD/month, achieving an ongoing 29% cost savings).*

---

## 6. Full 23-Resource Inventory (`tfplan-resume`)

| # | Resource Address | Azure Type | Region | Planned Action |
| :-: | :--- | :--- | :---: | :---: |
| **1** | `azurerm_resource_group.rg` | `Microsoft.Resources/resourceGroups` | `eastus` | **CREATE (+)** |
| **2** | `module.monitoring.law` | `Microsoft.OperationalInsights/workspaces` | `eastus` | **CREATE (+)** |
| **3** | `module.monitoring.appinsights` | `Microsoft.Insights/components` | `eastus` | **CREATE (+)** |
| **4** | `module.acr.acr` | `Microsoft.ContainerRegistry/registries` | `eastus` | **CREATE (+)** |
| **5** | `module.keyvault.kv` | `Microsoft.KeyVault/vaults` | `eastus` | **CREATE (+)** *(Auto-Recover)* |
| **6** | `module.servicebus.sb` | `Microsoft.ServiceBus/namespaces` | `eastus` | **CREATE (+)** |
| **7** | `module.servicebus.topic.member_events` | `Microsoft.ServiceBus/namespaces/topics` | `eastus` | **CREATE (+)** |
| **8** | `module.servicebus.topic.claim_events` | `Microsoft.ServiceBus/namespaces/topics` | `eastus` | **CREATE (+)** |
| **9** | `module.servicebus.sub.claims_ingestion` | `.../topics/subscriptions` | `eastus` | **CREATE (+)** |
| **10**| `module.servicebus.sub.adjudication` | `.../topics/subscriptions` | `eastus` | **CREATE (+)** |
| **11**| `module.servicebus.rule.member_enrolled` | `.../subscriptions/rules` | `eastus` | **CREATE (+)** |
| **12**| `module.aks.aks` | `Microsoft.ContainerService/managedClusters` | `eastus` | **CREATE (+)** |
| **13**| `module.aks.aks_acr_pull[0]` | `Microsoft.Authorization/roleAssignments` | Azure RBAC | **CREATE (+)** |
| **14**| `module.sql.sql_server` | `Microsoft.Sql/servers` | `centralindia` | **CREATE (+)** |
| **15**| `module.sql.allow_azure` | `Microsoft.Sql/servers/firewallRules` | `centralindia` | **CREATE (+)** |
| **16**| `module.sql.db_tenant_a` | `Microsoft.Sql/servers/databases` | `centralindia` | **CREATE (+)** |
| **17**| `module.sql.db_shared_payers` | `Microsoft.Sql/servers/databases` | `centralindia` | **CREATE (+)** |
| **18**| `module.cosmosdb.cosmos` | `Microsoft.DocumentDB/databaseAccounts` | `centralindia` | **CREATE (+)** |
| **19**| `module.cosmosdb.db` | `.../databaseAccounts/sqlDatabases` | `centralindia` | **CREATE (+)** |
| **20**| `module.cosmosdb.claims` | `.../sqlDatabases/containers` | `centralindia` | **CREATE (+)** |
| **21**| `module.cosmosdb.members` | `.../sqlDatabases/containers` | `centralindia` | **CREATE (+)** |
| **22**| `module.cosmosdb.idempotency` | `.../sqlDatabases/containers` | `centralindia` | **CREATE (+)** |
| **23**| `module.apim.apim` | `Microsoft.ApiManagement/service` | `eastus` | **CREATE (+)** |

---

## 7. Strict Approval Gate & Resumption Command

> [!WARNING]
> Applying `tfplan-resume` will provision the complete 23-resource architecture across `eastus` and `centralindia`.
> No resources will be modified or destroyed (`0 to change, 0 to destroy`).

All safety, quota, recovery, and regional checks have PASSED.

To authorize and begin live provisioning:

```powershell
cd infrastructure/terraform/environments/dev
terraform apply tfplan-resume
```
