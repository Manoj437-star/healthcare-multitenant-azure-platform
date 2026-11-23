# Azure Multi-Tenant Healthcare Microservices Platform

[![.NET 8](https://img.shields.io/badge/.NET-8.0%20LTS-blue.svg)](https://dotnet.microsoft.com/)
[![Kubernetes](https://img.shields.io/badge/Kubernetes-AKS-326CE5.svg)](https://azure.microsoft.com/en-us/products/kubernetes-service)
[![Terraform](https://img.shields.io/badge/Terraform-IaC-7B42BC.svg)](https://www.terraform.io/)
[![Helm](https://img.shields.io/badge/Helm-v3-0F1689.svg)](https://helm.sh/)
[![HIPAA Technical Safeguards](https://img.shields.io/badge/Compliance-HIPAA%20Aligned-green.svg)](https://www.hhs.gov/hipaa/index.html)
[![k6 Load Testing](https://img.shields.io/badge/Performance-k6%20Validated-orange.svg)](https://k6.io/)

A cloud-native, enterprise-hardened **Multi-Tenant Healthcare Payer Platform** on Microsoft Azure, engineered to support 50+ payer organizations processing up to 10 million claims per month with zero cross-tenant data leakage, strict HIPAA-oriented technical safeguards, and event-driven horizontal autoscaling.

---

## 1. Executive Summary & Architecture Highlights

The platform implements a **Practical Hybrid Multi-Tenancy Architecture**:
- **Compute Layer:** High-density, auto-scaled Azure Kubernetes Service (AKS) cluster with dedicated namespaces (`tenant-a`, `tenant-b`, `platform-shared`) micro-segmented by zero-trust Kubernetes `NetworkPolicy` objects.
- **Application Context Layer:** Defense-in-depth authorization where ASP.NET Core middleware and Entity Framework Core **Global Query Filters** enforce tenant data boundaries at the ORM layer, preventing accidental cross-tenant data access.
- **Data Persistence Tier:**
  - **Tier 1 (Enterprise Payers):** Dedicated Azure SQL Database per tenant (`tenant-a-db`) with Customer-Managed Keys (CMK).
  - **Tier 2 (Standard Payers):** Pooled Azure SQL Database (`shared-payers-db`) with Row-Level Security (RLS).
  - **Claims Document Store:** Azure Cosmos DB partitioned by `/tenantId` for sub-10ms point writes.
- **Enterprise Messaging:** Azure Service Bus (Premium) with topic subscriptions, Dead-Letter Queues (DLQ), and a **Transactional Outbox Pattern** guaranteeing atomicity between SQL commits and event publishing.
- **Zero-Trust API Gateway:** Azure API Management (APIM) enforcing OAuth 2.0 / JWT validation, tenant claim verification, anti-spoofing header checks, dynamic backend routing, and 1,000 req/min/tenant rate limiting.
- **Autoscaling:** KEDA (Kubernetes Event-driven Autoscaling) dynamically scaling Claims Ingestion pods from 2 to 20 replicas based on Azure Service Bus subscription backlog.
- **Analytical Read Model (HTAP):** Azure Synapse Serverless SQL pools reading Azure Cosmos DB Analytical Store (columnar format) with zero impact on operational Request Units (RUs).

---

## 2. Repository Layout

```
healthcare-multitenant-platform/
├── .github/workflows/
│   └── ci-cd.yml                          # GitHub Actions OIDC CI/CD Pipeline
├── analytics/
│   └── synapse/sql/                       # Synapse Serverless SQL credentials, views, RLS
├── apim/
│   ├── policies/
│   │   ├── global-policy.xml              # Global correlation ID & security headers
│   │   ├── member-service-policy.xml      # Member API JWT validation & tenant routing
│   │   └── claims-ingestion-policy.xml    # Claims API anti-spoofing & rate limiting
│   └── README.md                          # APIM deployment guide
├── docker/
│   ├── docker-compose.yml                 # Local dev orchestration (SQL, ASB, APIs, Grafana)
│   └── servicebus-emulator/Config.json    # Azure Service Bus Emulator entities
├── docs/
│   ├── architecture/
│   │   ├── tenant-isolation-strategy.md   # Silo vs Pooled vs Hybrid evaluation
│   │   ├── system-architecture.md         # Microservices platform architecture
│   │   ├── event-driven-architecture.md   # Service Bus messaging & outbox pattern
│   │   ├── cqrs-event-sourcing.md         # Adjudication CQRS & Cosmos DB store
│   │   ├── apim-topology.md               # Gateway ingress & anti-spoofing
│   │   ├── keda-autoscaling.md            # KEDA backlog scaling mathematics
│   │   └── synapse-analytical-read-model.md # HTAP analytics pipeline & lifecycle
│   ├── deployment/runbook.md              # Deployment & Troubleshooting runbook
│   └── security/
│       ├── hipaa-compliance-guide.md      # HIPAA technical controls matrix
│       ├── hipaa-security-risk-assessment.md # NIST/HIPAA SRA, STRIDE & asset inventory
│       ├── risk-register.md               # 12-item Enterprise Risk Register
│       └── baa-and-third-party-governance.md # BAA requirements & Breach Notification
├── infrastructure/
│   ├── powershell/
│   │   └── Onboard-Tenant.ps1             # 9-step automated tenant onboarding engine
│   └── terraform/
│       ├── environments/dev/              # Root Dev environment configuration
│       └── modules/                       # 9 Reusable Terraform modules (AKS, SQL, Synapse, etc.)
├── kubernetes/
│   ├── namespaces/namespaces.yaml         # Tenant-A, Tenant-B, shared namespaces
│   ├── network-policies/                  # Zero-trust micro-segmentation policies
│   ├── secrets/secret-provider-class.yaml # Key Vault Secrets Store CSI integration
│   ├── keda/                              # ScaledObject & TriggerAuthentication
│   └── helm/
│       ├── member-service/                # Member Service Helm chart
│       ├── claims-ingestion/              # Claims Ingestion Helm chart
│       ├── values-tenant-a.yaml           # Tier 1 Enterprise tenant values
│       └── values-tenant-b.yaml           # Tier 2 Standard tenant values
├── monitoring/
│   ├── prometheus/prometheus.yml          # Scrape configuration
│   ├── grafana/provisioning/              # Datasource & dashboard providers
│   ├── dashboards/
│   │   ├── healthcare-tenant-observability.json # Operational multi-tenant dashboard
│   │   └── claims-load-testing.json       # Real-time k6 load testing & KEDA dashboard
│   └── cost-governance/                   # FinOps T-SQL chargeback queries & Power BI spec
├── performance-testing/
│   ├── k6/                                # k6 test scenarios (Baseline, Sustained, Peak)
│   ├── locust/                            # Distributed Python Locust alternative
│   ├── results/performance-test-report.md # Comprehensive latency, KEDA & RU report
│   └── README.md                          # Load test execution guide
├── src/
│   ├── MemberService/                     # Member Enrollment API (.NET 8 + EF Core)
│   └── ClaimsIngestionService/            # Claims Intake API + Consumer (.NET 8 + Cosmos)
├── tests/
│   ├── MemberService.Tests/               # Unit tests (Enrollment, Outbox, Tenant filter)
│   ├── ClaimsIngestionService.Tests/      # Unit tests (JSON Schema, Idempotency, Anti-spoof)
│   └── IntegrationTests/                  # End-to-end integration & tenant isolation tests
├── scripts/
│   └── run-local.ps1                      # One-click local development bootstrapper
└── HealthcarePlatform.sln                 # Root solution file
```

---

## 3. Quickstart: Local Development

### 3.1 Start All Services Locally
```powershell
.\scripts\run-local.ps1 -Build
```

### 3.2 Access Local Interfaces
- **Member Service Swagger:** [http://localhost:5001/swagger](http://localhost:5001/swagger)
- **Claims Ingestion Swagger:** [http://localhost:5002/swagger](http://localhost:5002/swagger)
- **Grafana Dashboard:** [http://localhost:3000](http://localhost:3000) (`admin` / `admin`)
- **Prometheus UI:** [http://localhost:9090](http://localhost:9090)
- **SQL Server 2022:** `localhost:1433` (`sa` / `HealthcareStrongP@ss2026!`)

---

## 4. Enterprise Performance & Load Testing

Three non-equivalent load scenarios are provided under [`performance-testing/`](file:///d:/DevOps/healthcare-multitenant-platform/performance-testing/):

1. **Scenario A (Monthly Volume Baseline):** 10M claims/month (~231 claims/minute).
   ```bash
   k6 run performance-testing/k6/claims-ingestion.js
   ```
2. **Scenario B (High-Throughput Sustained):** 3,850 claims/minute (~64.2 claims/second).
   ```bash
   k6 run performance-testing/k6/claims-sustained.js
   ```
3. **Scenario C (Peak Burst & Recovery):** 15,000 claims/minute (~250 claims/second).
   ```bash
   k6 run performance-testing/k6/claims-peak.js
   ```

*Benchmark Summary:*
- Scenario A: p50 = 42ms | p95 = 88ms | p99 = 145ms | 100% success rate.
- Scenario B: p50 = 85ms | p95 = 195ms | p99 = 340ms | 99.94% success rate | 6 pods scaled by KEDA.
- Scenario C: p50 = 210ms | p95 = 520ms | p99 = 980ms | 99.12% success rate | 16 pods burst capacity.

See full analysis in [performance-test-report.md](file:///d:/DevOps/healthcare-multitenant-platform/performance-testing/results/performance-test-report.md).

---

## 5. Automated Tenant Onboarding

Onboard a new healthcare payer tenant with full compute, network, database, and gateway isolation:

```powershell
.\infrastructure\powershell\Onboard-Tenant.ps1 `
  -TenantId "tenant-c" `
  -TenantName "Horizon Health Payer" `
  -Environment "dev" `
  -DatabaseTier "Dedicated" `
  -NotificationEmail "admin@horizonhealth.com"
```

The script automatically executes:
1. Validates input patterns and Azure credentials.
2. Generates and creates the Kubernetes namespace with Pod Security admission labels.
3. Applies strict `NetworkPolicy` blocking all cross-tenant traffic.
4. Provisions the tenant database tier (Dedicated Azure SQL or RLS on pooled DB).
5. Configures Azure Key Vault secrets with rotation policies.
6. Registers the Microsoft Entra ID Application with tenant claims.
7. Generates APIM API subscriptions and rate-limiting rules.
8. Outputs an integration summary report.
9. Dispatches tenant welcome notification.

---

## 6. Security, HIPAA Safeguards & Governance

- **Zero-Trust Network Policies:** Pods in `tenant-a` are blocked by default from communicating with pods in `tenant-b`.
- **Anti-Spoofing Gateway:** If a client presents `X-Tenant-ID: tenant-b` while their JWT token belongs to `tenant-a`, the APIM gateway rejects the call immediately with HTTP 403 Forbidden.
- **ORM-Level Query Filtering:** Entity Framework Core enforces global query filters (`e => e.TenantId == _tenantContext.TenantId`) on all tenant entities.
- **Zero PHI in Observability:** Diagnostic metrics and log events strip all patient names, SSNs, and medical codes.
- **HIPAA Security Risk Assessment:** Detailed asset inventory, STRIDE threat model, and 12-item Risk Register documented in [`docs/security/`](file:///d:/DevOps/healthcare-multitenant-platform/docs/security/).
- **BAA & Incident Response:** 60-day federal reporting protocols and Azure BAA coverage documented in [baa-and-third-party-governance.md](file:///d:/DevOps/healthcare-multitenant-platform/docs/security/baa-and-third-party-governance.md).
