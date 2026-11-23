# Architecture Summary & Multi-Tenancy Strategy

## 1. Selected Hybrid Multi-Tenancy Isolation Model

The platform implements a **Practical Hybrid Isolation Architecture** balancing strict regulatory requirements (HIPAA/HITECH technical safeguards) with cloud unit economics across diverse payer profiles.

```mermaid
flowchart TD
    subgraph Gateway ["Perimeter Layer: Azure API Management"]
        APIM["API Gateway (APIM)
        - JWT Signature & Audience Check
        - Anti-Spoofing: X-Tenant-ID == JWT tenant_id
        - Rate Limit: 1000 req/min/tenant
        - Correlation ID Injection"]
    end

    subgraph Cluster ["Compute Layer: Azure Kubernetes Service (AKS)"]
        subgraph NamespaceA ["Namespace: tenant-a"]
            MemberSvcA["Member Service Pods (Tenant A)"]
            NetPolA["NetworkPolicy: Block all cross-namespace ingress/egress"]
        end

        subgraph NamespaceB ["Namespace: tenant-b"]
            MemberSvcB["Member Service Pods (Tenant B)"]
            NetPolB["NetworkPolicy: Block all cross-namespace ingress/egress"]
        end

        subgraph NamespaceShared ["Namespace: platform-shared"]
            ClaimsIngest["Claims Ingestion Service Pods (Shared High-Throughput Pool)"]
            KEDA["KEDA Autoscaler (Backlog Scaler: 2 to 20 Replicas)"]
        end
    end

    subgraph Messaging ["Messaging Layer: Azure Service Bus"]
        ASB_Member["Topic: member-events (MemberEnrolled)"]
        ASB_Claim["Topic: claim-events (ClaimReceived)"]
    end

    subgraph DataTier ["Persistence Tier"]
        SQL_Dedicated[("Azure SQL DB: tenant-a-db (Tier 1 Dedicated)")]
        SQL_Pooled[("Azure SQL DB: shared-payers-db (Tier 2 RLS Pooled)")]
        Cosmos[("Azure Cosmos DB: claims (/tenantId Partition)")]
        Synapse[("Azure Synapse Serverless SQL (Analytical Store)")]
    end

    APIM -->|Route tenant-a| MemberSvcA
    APIM -->|Route tenant-b| MemberSvcB
    APIM -->|Route claims| ClaimsIngest

    MemberSvcA -->|Transactional Outbox| SQL_Dedicated
    MemberSvcB -->|Transactional Outbox| SQL_Pooled
    MemberSvcA -.->|Publish Event| ASB_Member
    MemberSvcB -.->|Publish Event| ASB_Member

    ASB_Member -->|Consume MemberEnrolled| ClaimsIngest
    ClaimsIngest -->|Partitioned Ingest| Cosmos
    ClaimsIngest -.->|Publish ClaimReceived| ASB_Claim
    Cosmos -.->|Zero-RU Columnar Sync| Synapse
```

---

## 2. Multi-Layer Defense-in-Depth

1. **Perimeter Layer (APIM):** Validates authenticated Entra ID JWT claims (`tenant_id`, `tid`). If a caller supplies an unauthenticated or mismatched `X-Tenant-ID` header, APIM terminates the request immediately with HTTP 403 Forbidden.
2. **Network Layer (AKS):** Kubernetes CNI NetworkPolicies enforce default deny-all ingress and egress. Pods in `tenant-a` are blocked at the packet layer from communicating with pods in `tenant-b`.
3. **Application Context Layer (EF Core):** Microservice entities carry a `TenantId` attribute. Entity Framework Core applies a **Global Query Filter** (`e => e.TenantId == _tenantContext.TenantId`), preventing accidental cross-tenant data leakage if an application query lacks an explicit `WHERE` clause.
4. **Data Layer (Tiered SQL & Cosmos DB):**
   - Tier 1 Enterprise Payers receive dedicated Azure SQL databases with isolated backup chains and Customer-Managed Keys (CMK).
   - Tier 2 Standard Payers share pooled databases with Row-Level Security predicates (`SESSION_CONTEXT(N'TenantId') = TenantId`).
   - Claims intake utilizes Cosmos DB partitioned by `/tenantId` for sub-10ms point writes and predictable RU scaling.
5. **Messaging Layer (Transactional Outbox):** Ensures atomicity between database commits and event emissions without distributed two-phase commit overhead.
