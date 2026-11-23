# Azure Synapse Analytics Deployment & Verification Report

## 1. Executive Summary

This report documents the analytical read model deployment connecting **Azure Cosmos DB Analytical Store** (columnar Parquet format) to **Azure Synapse Analytics Serverless SQL Pools**.

The pipeline establishes a zero-ETL, Hybrid Transactional/Analytical Processing (HTAP) architecture allowing executive, clinical, and financial queries to execute without consuming operational Request Units (RUs).

---

## 2. Configuration & Compatibility Verification

| Component | Target Setting | Validation Status | Architecture Assessment |
| :--- | :--- | :--- | :--- |
| **Workspace Name** | `syn-healthcare-dev-01` | Configured | Azure Synapse Serverless SQL Endpoint |
| **Target Database** | `HealthcareAnalyticsDb` | Configured | Dedicated analytics database |
| **Cosmos DB Integration**| `claims` container (`analytical_storage_ttl = -1`) | Configured | Automatic, continuous columnar sync |
| **Authentication** | Managed Identity / Entra ID | Configured | Role: `Cosmos DB Built-in Data Reader` |
| **Security Pattern** | Inline Role/Session Views | Configured | Compensates for Serverless SQL lack of native UDF / RLS policies |

---

## 3. SQL Deployment Scripts (`analytics/synapse/sql/`)

| File Name | Purpose | Validation Status |
| :--- | :--- | :--- |
| **`01_create_credentials_and_datasource.sql`** | Creates database, schemas, master encryption key, and database-scoped credentials. | **Validated** |
| **`02_create_views.sql`** | Defines `analytics.ClaimsReportingView` via `OPENROWSET(PROVIDER = 'CosmosDB', ...)` with SHA-256 member anonymization. | **Validated** |
| **`03_analytical_queries.sql`** | Implements the 5 core analytical reports (Total claims, status distribution, monthly trends, provider network, executive KPIs). | **Validated** |
| **`04_tenant_security_rls.sql`** | Implements tenant-scoped views (`TenantAClaimsView`, `TenantBClaimsView`) and `SecureClaimsReportingView` with session context. | **Validated** |
| **`deploy_synapse.sql`** | Master SQLCMD deployment wrapper with `:on error exit` and sequential `:r` execution. | **Validated** |

---

## 4. Execution Runbook via `sqlcmd`

```powershell
# Authenticate and execute sequentially in SQLCMD mode:
cd analytics/synapse/sql
sqlcmd `
  -S "syn-healthcare-dev-01-ondemand.sql.azuresynapse.net" `
  -d "master" `
  -G -I `
  -i "deploy_synapse.sql" `
  -b
```
*(The `-b` flag terminates execution immediately if any step fails, preventing corrupt partial states).*

---

## 5. Tenant Isolation & Privacy Verification

- **Role-Based Isolation:** Queries by `TenantARole` against `TenantAClaimsView` return strictly `tenant-a` data. Attempts to read `TenantBClaimsView` fail with SQL permission error.
- **Session-Based Isolation:** Power BI Embedded sets `SESSION_CONTEXT(N'TenantId') = 'tenant-a'`, constraining returned result sets at the database engine level.
- **De-Identification:** Member identifiers are pseudonymized using SHA-256 (`HASH-A4F890C12E7B`), preventing PHI exposure in reporting datasets.
