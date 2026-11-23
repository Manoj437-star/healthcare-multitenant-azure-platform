# Tenant Isolation Strategy Evaluation & Design

## 1. Executive Summary

In a healthcare multi-tenant platform catering to 50+ payer organizations processing up to 10 million claims per month, data isolation, regulatory compliance (HIPAA/HITECH), cost efficiency, and operational scalability are paramount.

This document evaluates the three fundamental multi-tenancy models:
1. **Silo Model** (Complete physical/logical isolation per tenant)
2. **Pooled Model** (Shared infrastructure and data stores)
3. **Hybrid Model** (Tiered multi-tenancy balancing shared compute with segregated or pooled data)

**Decision for this Platform:** We adopt a **Practical Hybrid Isolation Model**. Large enterprise payers (Tier 1) receive dedicated Azure SQL databases and dedicated Kubernetes namespaces with strict network micro-segmentation, while smaller payers (Tier 2/Standard) share a pooled database cluster with row-level security (RLS) and shared namespaces, while compute workloads run on shared, auto-scaled AKS nodepools governed by Kubernetes NetworkPolicies and tenant-aware application contexts.

---

## 2. Comparative Evaluation Matrix

| Evaluation Dimension | Silo Model | Pooled Model | Hybrid Model (Selected) |
| :--- | :--- | :--- | :--- |
| **Security & Isolation** | **Highest** (Hard physical/logical boundaries per tenant) | **Moderate** (Relies exclusively on software-level isolation) | **High** (Defense-in-depth: NetworkPolicies + App authorization + dedicated/RLS data stores) |
| **Infrastructure Cost** | **High** (Resource underutilization, separate DBs & clusters) | **Lowest** (Maximum resource packing and economy of scale) | **Optimized** (Cost scaled with tenant tier and SLA guarantees) |
| **Scalability (10M Claims/mo)** | **Partitioned** (Each tenant scales independently, but management overhead explodes) | **Complex** (Hot tenant noisy-neighbor issues, table lock contention) | **Excellent** (High-throughput tenants isolated; auto-scaled KEDA workers scale compute dynamically) |
| **Blast Radius** | **Minimal** (Failure or breach limited to one tenant) | **Severe** (Single query spike or breach impacts all tenants) | **Constrained** (Tier 1 failure isolated; Tier 2 protected by circuit breakers and rate limits) |
| **Operational Complexity** | **Very High** (Managing 50+ DBs, 50+ clusters/namespaces, 50+ pipelines) | **Low** (Single schema, single deployment target) | **Balanced** (Automated Terraform & PowerShell onboarding scripts manage tenant lifecycle) |
| **HIPAA Compliance** | **Easiest to audit** (Physical/logical separation satisfies stringent BAA clauses) | **Rigorous audit required** (Must prove zero data co-mingling risk under HIPAA Security Rule § 164.312) | **Highly compliant** (Enables Tenant-specific encryption keys [BYOK], isolated audit logs, and granular BAAs) |

---

## 3. Deep-Dive Model Analysis

### 3.1 Silo Model
- **Compute:** Dedicated AKS cluster or dedicated node pool + namespace per tenant.
- **Data:** Dedicated Azure SQL server and database, dedicated Cosmos DB account per tenant.
- **Networking:** Dedicated VNet/Subnet with dedicated NSGs and Private Endpoints.
- **Secrets:** Dedicated Azure Key Vault per tenant.
- **Pros:** Maximum security, zero noisy neighbor risk, simplified per-tenant cost attribution, clean BAA compliance.
- **Cons:** Astronomical Azure infrastructure cost, idle capacity waste, massive DevOps maintenance burden (50+ migrations per release).

### 3.2 Pooled Model
- **Compute:** Shared AKS nodepools and single shared namespace.
- **Data:** Single Azure SQL database with `TenantId` column on every table; single Cosmos DB container partitioned by `/TenantId`.
- **Networking:** Flat internal network with no tenant-level network boundaries.
- **Pros:** Lowest possible cost, trivial deployment, centralized operations.
- **Cons:** High risk of cross-tenant data leakage if an application bug omits `TenantId` in SQL `WHERE` clause; noisy neighbors can exhaust connection pools; difficult to offer custom data retention or Bring-Your-Own-Key (BYOK) encryption to enterprise payers.

### 3.3 Selected: Practical Hybrid Model
The healthcare payer market features diverse tenant profiles: large national health plans with stringent compliance teams demanding dedicated databases and cryptographic isolation, alongside regional plans and third-party administrators (TPAs) seeking cost-effective solutions.

#### Key Architectural Tenets of the Hybrid Model:
1. **Compute Layer (Shared & Segmented):**
   - High-density AKS cluster utilizing Linux virtual node autoscaling.
   - Logical isolation via dedicated Kubernetes namespaces (`tenant-a`, `tenant-b`) for tenant-dedicated workloads, alongside a `platform-core` namespace for shared ingestion.
   - Kubernetes `NetworkPolicy` objects enforcing zero-trust: pods in `tenant-a` are blocked by default from communicating with pods in `tenant-b`.
2. **Data Layer (Tiered):**
   - **Tier 1 (Enterprise Payers):** Dedicated Azure SQL Database per tenant. Completely segregated database file, backup chain, and independent compute scaling.
   - **Tier 2 (Standard Payers):** Pooled Azure SQL Database leveraging SQL Server **Row-Level Security (RLS)** and tenant security predicates (`SESSION_CONTEXT(N'TenantId') = TenantId`).
   - **Claims Document Store (Azure Cosmos DB):** Logical partitioning using `/tenantId` as the partition key. Cosmos DB guarantees physical partition isolation when partition size expands, providing sub-10ms point reads and predictable Request Unit (RU) allocation.
3. **Application Layer (Context-Driven Isolation):**
   - Never rely solely on Kubernetes namespaces for security.
   - Every HTTP request must supply a valid JWT with tenant claims (`tid`, `tenant_id`).
   - ASP.NET Core middleware extracts and validates the tenant against the token signature.
   - Entity Framework Core registers a Global Query Filter (`e => e.TenantId == _tenantContext.TenantId`) on all tenant entities, ensuring forgotten `WHERE` clauses cannot leak data.
4. **Message Broker Layer (Azure Service Bus):**
   - Shared Service Bus Namespace with domain-specific Topics (`member-events`, `claim-events`).
   - Subscription rules with SQL Filter Expressions (`user.TenantId = 'tenant-a'`) allow tenant-specific consumers while shared consumers process all tenants efficiently using correlation IDs.

---

## 4. Defense-in-Depth Isolation Diagram

```mermaid
flowchart TD
    subgraph ClientLayer ["Client Layer"]
        PayerA["Payer A (Tenant-A) Web/EDI"]
        PayerB["Payer B (Tenant-B) Web/EDI"]
    end

    subgraph GatewayLayer ["API Gateway (Azure API Management)"]
        APIM["Azure API Management Gateway"]
        APIMPolicy["APIM Inbound Policy:
        - JWT Signature & Expiry Check
        - Extract 'tenant_id' from JWT
        - Match X-Tenant-ID Header == JWT tenant_id
        - Rate Limit: 1000 req/min/tenant
        - Inject Correlation ID"]
    end

    subgraph NetworkLayer ["Kubernetes Network Isolation (AKS)"]
        subgraph NamespaceA ["Namespace: tenant-a"]
            MemberSvcA["Member Service (Tenant-A)"]
            NetPolA["NetworkPolicy: Deny Ingress from other namespaces"]
        end

        subgraph NamespaceB ["Namespace: tenant-b"]
            MemberSvcB["Member Service (Tenant-B)"]
            NetPolB["NetworkPolicy: Deny Ingress from other namespaces"]
        end

        subgraph NamespaceIngestion ["Namespace: platform-shared"]
            ClaimsIngest["Claims Ingestion Service (Shared Worker Pool)"]
            KEDA["KEDA Autoscaler (Service Bus Queue Lag)"]
        end
    end

    subgraph MessagingLayer ["Enterprise Event Broker"]
        ASB["Azure Service Bus (Premium)
        Topics: member-events, claim-events
        Filters: CorrelationId, TenantId"]
    end

    subgraph DataLayer ["Data Isolation Tier"]
        subgraph DedicatedDB ["Tier 1: Dedicated Databases"]
            SQLA[("Azure SQL DB (tenant-a-db)")]
        end
        subgraph PooledDB ["Tier 2: Pooled with RLS"]
            SQLPooled[("Azure SQL DB (shared-payers-db)
            [Row-Level Security Filter]")]
        end
        subgraph NoSQLStore ["Cosmos DB Document Store"]
            CosmosClaims[("Cosmos DB: claims-container
            Partition Key: /tenantId")]
        end
    end

    PayerA -->|Bearer Token (Tenant-A)| APIM
    PayerB -->|Bearer Token (Tenant-B)| APIM
    APIM --> APIMPolicy
    APIMPolicy -->|Route to tenant-a| MemberSvcA
    APIMPolicy -->|Route to tenant-b| MemberSvcB
    APIMPolicy -->|Route to Ingestion| ClaimsIngest

    MemberSvcA -->|Outbox Pattern| SQLA
    MemberSvcB -->|Outbox Pattern| SQLPooled
    MemberSvcA -.->|Publish MemberEnrolled| ASB
    MemberSvcB -.->|Publish MemberEnrolled| ASB

    ClaimsIngest -->|Consume Events| ASB
    ClaimsIngest -->|Partitioned Writes| CosmosClaims
    ClaimsIngest -.->|Publish ClaimReceived| ASB
    KEDA -->|Monitors Lag| ASB
    KEDA -->|Scales Pods| ClaimsIngest
```

---

## 5. Regulatory & Compliance Implications (HIPAA/HITECH)

1. **Business Associate Agreements (BAA):**
   Microsoft Azure provides an enterprise BAA covering AKS, Azure SQL, Cosmos DB, Service Bus, and APIM.
2. **Access Control (§ 164.312(a)(1)):**
   Zero cross-tenant visibility. All access requires Microsoft Entra ID authentication and role-based access control (RBAC).
3. **Audit Controls (§ 164.312(b)):**
   Tenant-tagged structured logging. All data access events emit log streams containing `tenant_id`, `user_id`, and `correlation_id` to Azure Log Analytics Workspace with immutable retention.
4. **Data at Rest & in Transit (§ 164.312(a)(2)(iv) & § 164.312(e)(1)):**
   - In-transit: TLS 1.3 enforced on APIM external interfaces; TLS 1.2+ minimum on Azure PaaS.
   - At-rest: Transparent Data Encryption (TDE) with customer-managed keys (CMK) in Azure Key Vault for Tier 1 tenants; Microsoft-managed keys for Tier 2.
