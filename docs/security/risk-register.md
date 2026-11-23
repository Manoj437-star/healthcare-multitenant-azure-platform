# Enterprise HIPAA Security Risk Register

## 1. Risk Scoring Methodology

Risk scores are evaluated using standard NIST SP 800-30 and healthcare risk matrix standards:
- **Likelihood:** 1 (Rare), 2 (Unlikely), 3 (Possible), 4 (Likely), 5 (Almost Certain)
- **Impact:** 1 (Insignificant), 2 (Minor), 3 (Moderate), 4 (Major), 5 (Catastrophic)
- **Severity Rating:**
  - **Critical (20–25):** Immediate executive mitigation required prior to production processing.
  - **High (12–19):** Active mitigation required within 30 days.
  - **Medium (6–11):** Managed under standard change management.
  - **Low (1–5):** Accepted residual risk.

---

## 2. Risk Register

| Risk ID | Risk Description | Affected Component | Severity | Likelihood | Impact | Mitigation Strategy | Owner | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **RSK-01** | Cross-tenant data leakage via forged HTTP tenant header (`X-Tenant-ID`). | API Management Gateway | **Critical (20)** | 4 | 5 | Inbound APIM XML policy extracts JWT token claims (`tenant_id`, `tid`) and verifies that `X-Tenant-ID` matches authenticated token before routing. Mismatches return HTTP 403. | Lead Security Architect | **Mitigated (Verified)** |
| **RSK-02** | Accidental cross-tenant query execution due to missing application `WHERE` clause. | Member Service (EF Core) | **High (16)** | 4 | 4 | Configured Entity Framework Core Global Query Filter (`e => e.TenantId == _tenantContext.TenantId`) on all tenant entities, ensuring automatic isolation at the ORM layer. | Lead Backend Architect | **Mitigated (Verified)** |
| **RSK-03** | Ingestion of malformed or corrupt healthcare claims poisoning downstream adjudication. | Claims Ingestion API | **High (15)** | 5 | 3 | Enforce strict JSON Schema validation (`JsonSchema.Net`) validating date formats, diagnosis codes, and amounts prior to Cosmos DB write. Poison messages DLQ'd after 5 retries. | Lead Software Engineer | **Mitigated (Verified)** |
| **RSK-04** | Hot physical partition in Cosmos DB when a Tier 1 payer bursts past 10,000 RU/s. | Azure Cosmos DB (`claims`) | **High (16)** | 4 | 4 | Transition from single `/tenantId` key to hierarchical partition keys (`/tenantId, /serviceDate`) to distribute physical partitions across time windows. | Principal Database Architect | **In Progress** |
| **RSK-05** | Accidental logging of Protected Health Information (PHI) in centralized observability logs. | Microservices Logging & Grafana | **High (16)** | 4 | 4 | Implemented Serilog sanitizing middleware stripping request bodies on `/api/members` and `/api/claims`. Prometheus metric labels strictly omit member IDs or diagnosis codes. | Lead SRE Engineer | **Mitigated (Verified)** |
| **RSK-06** | Denial of Service (DoS) caused by noisy neighbor batch claim submission. | AKS Compute & Ingestion Pods | **High (12)** | 3 | 4 | Configured per-tenant APIM rate-limiting policy (1,000 req/min/tenant) and KEDA autoscaling (2 to 20 replicas) based on Service Bus backlog. | Cloud Platform Engineer | **Mitigated (Verified)** |
| **RSK-07** | Compromise of database credentials stored in source code or plain Kubernetes YAML. | Microservice Pods & AKS | **Critical (20)** | 4 | 5 | Staged secrets in Azure Key Vault with soft-delete and purge protection. Injected secrets dynamically in-memory via Secrets Store CSI Driver and Azure Workload Identity. | DevOps Lead | **Mitigated (Verified)** |
| **RSK-08** | Interception of unencrypted claims data traversing internal cluster pod network. | Kubernetes Pod-to-Pod Traffic | **Medium (9)** | 3 | 3 | Enforced Kubernetes NetworkPolicies blocking cross-namespace traffic; Azure CNI encryption enabled for all inter-node pod traffic. | Kubernetes Specialist | **Mitigated (Verified)** |
| **RSK-09** | Unauthorized analytical reporting access exposing cross-tenant financial liabilities. | Azure Synapse / Power BI | **High (12)** | 3 | 4 | Implemented Row-Level Security (RLS) security predicate function in Synapse Serverless SQL views, mapping `SESSION_CONTEXT(N'TenantId')` to caller identity. | Analytics Architect | **Mitigated (Verified)** |
| **RSK-10** | Loss of transactional consistency between SQL database commit and Service Bus publish. | Member Service Event Publishing | **High (15)** | 3 | 5 | Implemented Transactional Outbox Pattern: member record and outbox event are saved within a single ACID transaction; background worker dispatches events reliably. | Backend Lead | **Mitigated (Verified)** |
| **RSK-11** | Failure of automated secret rotation leading to service outage. | Key Vault CSI Driver | **Medium (8)** | 2 | 4 | Configured `secret_rotation_interval = "2m"` in AKS Key Vault Secrets Provider addon with automated health probes alerting on mount failures. | DevOps Lead | **Mitigated (Verified)** |
| **RSK-12** | Cloud provider infrastructure security breach or hardware vulnerability. | Microsoft Azure PaaS / IaaS | **Medium (6)** | 1 | 5 | Executed enterprise Microsoft Business Associate Agreement (BAA), enabled Customer-Managed Keys (CMK) via Key Vault, and activated Microsoft Defender for Cloud. | Chief Information Security Officer (CISO) | **Managed (Contractual)** |
