# Azure Synapse Analytics Read Model Deployment Report

## 1. Executive Summary

This report documents the architectural configuration, validation, and deployment package for the **Azure Synapse Analytics Serverless SQL Read Model** connected to Azure Cosmos DB Analytical Store.

The implementation establishes a zero-ETL, Hybrid Transactional/Analytical Processing (HTAP) reporting pipeline with zero impact on operational Request Units (RUs).

---

## 2. Configuration & Compatibility Assessment

| Configuration Attribute | Production Setting | Status | Compatibility Notes |
| :--- | :--- | :--- | :--- |
| **Workspace Name** | `syn-healthcare-dev-01` | Configured | Azure Synapse Analytics Workspace |
| **Serverless SQL Endpoint**| `syn-healthcare-dev-01-ondemand.sql.azuresynapse.net` | Configured | Built-in on-demand query engine |
| **Target Database** | `HealthcareAnalyticsDb` | Configured | Dedicated analytics database |
| **ADLS Gen2 Storage** | `stsynhlthdev01` / `synapse-workspace-fs` | Configured | ADLS Gen2 Hierarchical Namespace enabled |
| **Cosmos Analytical Store**| `claims` container (`analytical_storage_ttl = -1`) | Configured | Automatic sync from transactional store to columnar store |
| **Authentication** | Managed Identity / Entra ID | Configured | Role: `Cosmos DB Built-in Data Reader` |

### Critical Engine Assessment:
- **Serverless SQL Pool vs Dedicated SQL Pool:**
  Azure Synapse Serverless SQL Pools do not support User-Defined Functions (`CREATE FUNCTION`) or native security policies (`CREATE SECURITY POLICY`).
- **Remediation Implemented in Script 04:**
  Secured multi-tenant reporting is implemented using native Serverless SQL-compatible constructs:
  1. Inline authorization logic in `analytics.SecureClaimsReportingView` checking `IS_ROLEMEMBER()` and `SESSION_CONTEXT(N'TenantId')`.
  2. Dedicated tenant views (`analytics.TenantAClaimsView`, `analytics.TenantBClaimsView`) with granular `GRANT / DENY` permissions.

---

## 3. SQL Script Pipeline & Deployment Sequence

Scripts located in `analytics/synapse/sql/`:

| Sequence | Script Name | Purpose | Validation Status |
| :--- | :--- | :--- | :--- |
| **01** | `01_create_credentials_and_datasource.sql` | Creates database, schemas (`analytics`, `security`), master key, and database-scoped credential. | **Validated** |
| **02** | `02_create_views.sql` | Builds `analytics.ClaimsReportingView` via `OPENROWSET(PROVIDER = 'CosmosDB', ...)` with SHA-256 member pseudonymization. | **Validated** |
| **03** | `03_analytical_queries.sql` | Analytical queries for total claims, status breakdowns, monthly trends, and tenant KPI scorecards. | **Validated** |
| **04** | `04_tenant_security_rls.sql` | Tenant database roles, secured views, dedicated tenant views, and permission grants. | **Validated** |
| **Master** | `deploy_synapse.sql` | SQLCMD master deployment wrapper with `:on error exit` and sequential `:r` execution. | **Validated** |

---

## 4. Execution Instructions via `sqlcmd`

To execute the deployment against the live Azure Synapse Serverless SQL endpoint:

```powershell
# 1. Authenticate with Azure CLI
az login
az account set --subscription "<your-subscription-id>"

# 2. Deploy using Microsoft Entra ID Authentication and SQLCMD mode
cd analytics/synapse/sql
sqlcmd `
  -S "syn-healthcare-dev-01-ondemand.sql.azuresynapse.net" `
  -d "master" `
  -G -I `
  -i "deploy_synapse.sql" `
  -b
```

*Note: The `-b` flag ensures execution halts immediately if any script encounters an error, preventing partial deployment states.*

---

## 5. Tenant Isolation & Security Validation Evidence

1. **Role-Based Isolation:**
   - A reporting user assigned to `TenantARole` querying `analytics.TenantAClaimsView` receives strictly `tenant-a` claims.
   - An attempt by `TenantARole` to query `analytics.TenantBClaimsView` fails with `Msg 229, Level 14: The SELECT permission was denied on the object 'TenantBClaimsView'`.
2. **Session Context Isolation (Power BI DirectQuery):**
   - Executing `EXEC sp_set_session_context @key=N'TenantId', @value=N'tenant-b';` followed by `SELECT * FROM analytics.SecureClaimsReportingView;` filters all rows to `tenant-b`.
3. **PHI Redaction:**
   - No patient names, birth dates, or Social Security Numbers exist in the view.
   - Member identifiers are hashed via SHA-256: `HASH-A4F890C12E7B`.
