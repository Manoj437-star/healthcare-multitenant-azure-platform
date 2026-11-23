# End-to-End System Architecture

## 1. System Overview

The **Azure Multi-Tenant Healthcare Microservices Platform** provides a secure, cloud-native, and horizontally scalable backplane for health plan operations (payer value chain). It is architected to support 50+ payer organizations processing up to 10 million claims per month with sub-second ingestion, deterministic event routing, and strict tenant isolation.

---

## 2. Microservices Architecture Map

```mermaid
graph TB
    subgraph IngressGateway ["API Management Gateway (APIM)"]
        APIM_GW["Azure API Management
        - OAuth2 / JWT Validation (Entra ID)
        - Tenant Context Resolver
        - Rate Limiter (1000 req/min/tenant)
        - Correlation ID & Trace Propagation"]
    end

    subgraph CoreServices ["POC Scope Services (Fully Implemented)"]
        subgraph SvcMember ["1. Member Service"]
            MS_API["Member API (ASP.NET Core)
            - Member Enrollment & Eligibility
            - Tenant-Scoped EF Core
            - Transactional Outbox Pattern"]
            MS_Worker["Outbox Dispatcher Worker"]
        end

        subgraph SvcClaimsIngest ["2. Claims Ingestion Service"]
            CI_API["Claims Ingestion API (ASP.NET Core)
            - JSON Schema Validation (X12-837 aligned)
            - Tenant Identity Enforcement
            - Cosmos DB Storage"]
            CI_Consumer["Member Event Consumer (BackgroundService)"]
        end
    end

    subgraph FutureServices ["Platform Extension Services (Architectural Design)"]
        subgraph SvcAdjudication ["3. Claims Adjudication Engine"]
            CA_Worker["Adjudication Worker
            - CQRS & Event Sourcing
            - Medical Necessity Rules Engine
            - Benefit Accumulator
            - Co-pay / Deductible Calculation"]
        end

        subgraph SvcProvider ["4. Provider Directory Service"]
            PDS_API["Provider Directory API
            - National Provider Identifier (NPI) Registry
            - In-Network / Out-of-Network Contracts
            - Specialty & Taxonomy Validation"]
        end

        subgraph SvcCareMgmt ["5. Care Management Service"]
            CMS_API["Care Management API
            - Chronic Disease Programs
            - Prior Authorization Tracking
            - High-Risk Member Outreach"]
        end

        subgraph SvcNotification ["6. Notification Service"]
            NS_Worker["Notification Dispatcher
            - Real-Time Alerts (Email / SMS / Webhooks)
            - HIPAA PHI Redaction Filter
            - Tenant White-Label Templates"]
        end
    end

    subgraph EventBackbone ["Event Backbone (Azure Service Bus Premium)"]
        Topic_Member["Topic: member-events
        (MemberEnrolled)"]
        Topic_Claim["Topic: claim-events
        (ClaimReceived, ClaimAdjudicated)"]
        Topic_Notification["Topic: notification-events"]
    end

    subgraph DataStorage ["Data Persistence Tier"]
        SQL_Member[("Azure SQL Database
        - Dedicated Tier 1 / Pooled Tier 2
        - Member & Eligibility Records
        - Outbox Messages")]
        Cosmos_Claims[("Azure Cosmos DB
        - Claims Store (/tenantId partition)
        - Event Sourcing Store
        - Sub-10ms Point Writes")]
        SQL_ReadModel[("Azure Synapse / SQL Read Model
        - Projected Claim Views
        - Analytical Reporting")]
        Cache_Redis[("Azure Cache for Redis
        - Provider Network Caching
        - Rate Limit Counter Cache")]
    end

    %% Ingress routes
    APIM_GW -->|/api/members| MS_API
    APIM_GW -->|/api/claims| CI_API
    APIM_GW -->|/api/providers| PDS_API
    APIM_GW -->|/api/care| CMS_API

    %% Member service dependencies
    MS_API --> SQL_Member
    MS_API --> MS_Worker
    MS_Worker -->|Publish MemberEnrolled| Topic_Member

    %% Claims ingestion dependencies
    Topic_Member -->|Consume MemberEnrolled| CI_Consumer
    CI_Consumer -.->|Update Member Cache| CI_API
    CI_API --> Cosmos_Claims
    CI_API -->|Publish ClaimReceived| Topic_Claim

    %% Adjudication dependencies
    Topic_Claim -->|Consume ClaimReceived| CA_Worker
    CA_Worker --> Cosmos_Claims
    CA_Worker --> Cache_Redis
    CA_Worker -->|Project Claim State| SQL_ReadModel
    CA_Worker -->|Publish ClaimAdjudicated| Topic_Claim
    CA_Worker -->|Trigger Member Notice| Topic_Notification

    %% Notification dependencies
    Topic_Notification --> NS_Worker
    Topic_Member --> NS_Worker
```

---

## 3. Microservice Specifications

### 3.1 Member Service (POC Implemented)
- **Role:** Master record management for health plan members, policy coverages, and eligibility periods.
- **Protocol:** REST API over HTTPS (`/api/members`).
- **Data Store:** Azure SQL Database using Entity Framework Core.
  - Dedicated database per Tier 1 tenant (`tenant-a-db`).
  - Shared database with Row-Level Security for Tier 2 tenants (`shared-payers-db`).
- **Reliability Pattern:** Transactional Outbox Pattern ensures atomicity between SQL persistence and message emission.
- **Events Published:** `MemberEnrolled`.

### 3.2 Claims Ingestion Service (POC Implemented)
- **Role:** High-throughput intake gateway for healthcare claims submitted by providers or clearinghouses.
- **Protocol:** REST API over HTTPS (`/api/claims`) and asynchronous Service Bus consumer.
- **Validation:** JSON Schema validation enforcing required healthcare claim structures (Member ID, Provider NPI, Service Date, Procedure Codes, Diagnosis Codes, Claim Amount).
- **Data Store:** Azure Cosmos DB (`claims-container`, Partition Key: `/tenantId`).
- **Events Consumed:** `MemberEnrolled` (maintains local cache of valid member IDs per tenant for zero-latency pre-validation).
- **Events Published:** `ClaimReceived`.
- **Autoscaling:** KEDA scaled on Service Bus topic subscription backlog.

### 3.3 Claims Adjudication Engine (Architecture / Future Extension)
- **Role:** Automated rules evaluation engine that assesses claim validity against member benefits, provider contracts, deductibles, and co-insurance.
- **Pattern:** CQRS and Event Sourcing. Every adjudication decision (Approved, Denied, Pended, Adjusted) is recorded as an immutable event in Cosmos DB.
- **Projection:** Synapse/SQL read models project the current claim state for query and claims portal operations.

### 3.4 Provider Directory Service (Future Extension)
- **Role:** Registry of credentialed healthcare providers, facilities, billing tax IDs, and network participation agreements.
- **Data Store:** Azure SQL with Azure Cache for Redis for sub-millisecond network tier lookups.

### 3.5 Care Management Service (Future Extension)
- **Role:** Identifies high-risk members based on claim diagnostic patterns and triggers care plans, case manager assignments, and disease management programs.
- **Event-Driven:** Consumes `ClaimAdjudicated` events from Service Bus.

### 3.6 Notification Service (Future Extension)
- **Role:** Enterprise notification dispatch for Explanation of Benefits (EOB), enrollment confirmation, and provider remittance notices via SendGrid and Azure Communication Services.

### 3.7 API Gateway (Azure API Management)
- **Role:** Single ingress point. Handles authentication, tenant claim extraction, tenant header validation, tenant-aware routing, rate limiting, and observability instrumentation.
