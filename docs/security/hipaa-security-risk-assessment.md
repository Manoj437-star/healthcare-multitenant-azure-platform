# HIPAA Security Risk Assessment (SRA) & Technical Readiness Specification

## 1. Executive Summary & Purpose

Under the **Health Insurance Portability and Accountability Act (HIPAA)** Security Rule (45 CFR § 164.308(a)(1)(ii)(A)), covered entities and business associates must conduct an accurate and thorough assessment of the potential risks and vulnerabilities to the confidentiality, integrity, and availability of electronic protected health information (**ePHI**).

> [!IMPORTANT]
> **Technical Readiness Declaration:** This document represents a rigorous **technical security readiness assessment** of the platform's architectural, cryptographic, and operational safeguards. It does not constitute a formal legal declaration of HIPAA certification, which requires organizational administrative policies, workforce training, continuous auditing, and executed Business Associate Agreements (BAAs).

---

## 2. Asset Inventory & System Boundaries

| Asset ID | Asset Name | Technology / Service | Data Processed / Stored | Boundary Isolation |
| :--- | :--- | :--- | :--- | :--- |
| **AST-01** | API Gateway | Azure API Management (APIM) | Inbound HTTPS requests, JWT tokens, tenant headers | VNet integrated, mTLS, TLS 1.3 |
| **AST-02** | Member Service Pods | ASP.NET Core 8 on AKS | Member enrollment payloads, demographics, coverage dates | Dedicated Linux namespace (`tenant-a`, `tenant-b`), non-root container |
| **AST-03** | Claims Ingestion Pods | ASP.NET Core 8 on AKS | Claims JSON payloads, procedure & diagnosis codes | `platform-shared` namespace, non-root container |
| **AST-04** | Relational Database | Azure SQL Database | Member master records, outbox events, policy metadata | TDE (CMK), Private Endpoint, Row-Level Security |
| **AST-05** | Document Store | Azure Cosmos DB | Claims documents, eligibility cache, idempotency records | AES-256 rest encryption, `/tenantId` partition key |
| **AST-06** | Enterprise Message Broker | Azure Service Bus (Premium) | `MemberEnrolled`, `ClaimReceived` event messages | TLS 1.2+ AMQP, topic subscription SQL filters, DLQ |
| **AST-07** | Cryptographic Vault | Azure Key Vault | Database connection strings, encryption keys, service principal secrets | Soft-delete, purge protection, RBAC, CSI Driver |
| **AST-08** | Analytical Data Store | Azure Synapse / ADLS Gen2 | Pseudonymized claim summaries, trend metrics | Private Link, no direct PHI, Columnar analytical store |
| **AST-09** | CI/CD Pipeline | GitHub Actions | Source code, container images, Helm charts | OIDC Passwordless authentication to Azure |

---

## 3. Data Classification Framework

All platform data is categorized into five sensitivity tiers:

1. **Tier 1: Electronic Protected Health Information (ePHI) [RESTRICTED]:**
   - *Examples:* Diagnosis codes (ICD-10), procedure codes (CPT), patient identifiers paired with healthcare provider IDs, claim amounts paired with clinical descriptions.
   - *Safeguards:* Mandatory encryption at rest (AES-256) and in transit (TLS 1.3/1.2), zero retention in unencrypted memory, strict audit trail, zero logging in diagnostic logs.
2. **Tier 2: Personally Identifiable Information (PII) [CONFIDENTIAL]:**
   - *Examples:* Member name, date of birth, home address, email, telephone number.
   - *Safeguards:* Encrypted in SQL tables, masked in user interfaces, excluded from operational telemetry.
3. **Tier 3: Multi-Tenant Operational Metadata [RESTRICTED INTERNAL]:**
   - *Examples:* `tenant_id`, `correlation_id`, `claim_id` (UUID), `service_date`.
   - *Safeguards:* Transmitted in headers, safe for diagnostic logs and metrics labels.
4. **Tier 4: Anonymized / Aggregated Research Data [INTERNAL]:**
   - *Examples:* Total claims per month, average claim amount by plan code, error rate percentages.
   - *Safeguards:* Safe for shared analytics dashboards and executive KPIs.
5. **Tier 5: Public Data [PUBLIC]:**
   - *Examples:* System documentation, open API Swagger specs.

---

## 4. End-to-End ePHI Data Flow Analysis

```mermaid
flowchart TD
    Client["Healthcare Client / EHR / Payer Web"]

    subgraph Ingress ["1. Ingress & Perimeter"]
        APIM["Azure API Management Gateway
        - TLS 1.3 Termination
        - JWT Signature & Expiry Check
        - Anti-Spoofing: X-Tenant-ID == JWT Claim
        - Strips Raw Payload from Gateway Logs"]
    end

    subgraph Compute ["2. Microservices Compute (AKS)"]
        MemberAPI["Member Service Pod
        - In-Memory Validation
        - EF Core Global Query Filter
        - Serializes to SQL & Outbox"]
        ClaimsAPI["Claims Ingestion Pod
        - JSON Schema Validation
        - In-Memory Schema Validation
        - Emits to Cosmos & Service Bus"]
    end

    subgraph MessageBroker ["3. Asynchronous Messaging"]
        ASB["Azure Service Bus (TLS Encrypted)
        - Topic: member-events
        - Topic: claim-events
        - DLQ for Poison Messages"]
    end

    subgraph DataAtRest ["4. Data at Rest (AES-256 Encrypted)"]
        SQL[("Azure SQL DB
        - Transparent Data Encryption (CMK)
        - Dedicated Tier 1 / RLS Tier 2
        - Outbox Table")]
        Cosmos[("Azure Cosmos DB
        - Partition Key: /tenantId
        - Encrypted Storage Keys")]
    end

    subgraph Analytics ["5. De-Identified Analytical Read Model"]
        Synapse["Azure Synapse Serverless SQL
        - Synapse Link Zero-RU Auto-Sync
        - Pseudonymized SHA-256 Member Key
        - Row-Level Security (RLS)"]
    end

    Client -->|1. HTTPS Request with Bearer Token| APIM
    APIM -->|2. Internal VNet Call| MemberAPI
    APIM -->|2. Internal VNet Call| ClaimsAPI

    MemberAPI -->|3. ACID Commit| SQL
    MemberAPI -.->|4. Outbox Worker Publish| ASB
    ASB -->|5. Consume MemberEnrolled| ClaimsAPI
    ClaimsAPI -->|6. Partitioned Write| Cosmos
    ClaimsAPI -.->|7. Publish ClaimReceived| ASB

    Cosmos -.->|8. Columnar Sync (No PHI)| Synapse
```

---

## 5. STRIDE Threat Model & Mitigations

| STRIDE Category | Threat Description | Vulnerable Surface | Platform Architectural Mitigation |
| :--- | :--- | :--- | :--- |
| **Spoofing** | Attacker impersonates a payer organization by sending a forged `X-Tenant-ID` header. | API Management Ingress | **Anti-Spoofing Policy:** APIM verifies authenticated Microsoft Entra ID JWT signature and enforces that client header strictly matches token claim `tenant_id`. Rejects mismatches with HTTP 403. |
| **Tampering** | Malicious actor modifies claim payload in flight between microservices. | In-cluster pod-to-pod communication | **mTLS & NetworkPolicies:** Kubernetes CNI network micro-segmentation blocks cross-namespace traffic; Istio/Azure CNI WireGuard secures pod traffic in transit. |
| **Repudiation** | Disputed claim adjudication decision cannot be proven or traced. | Claims Adjudication Engine | **Cosmos DB Event Sourcing:** Immutable event append log records every transition, version, timestamp, and user context. Records cannot be overwritten or deleted. |
| **Information Disclosure** | Application logs accidentally expose patient names, SSNs, or clinical diagnoses in Grafana/Log Analytics. | Diagnostic Logging & Metrics | **Sanitization Filters:** Microservice logging filters strip request bodies on `/api/members` and `/api/claims`. Prometheus metric labels strictly omit member IDs and medical codes. |
| **Denial of Service** | High-volume batch ingestion from Tenant A exhausts compute and starves Tenant B. | Claims Ingestion API & Pods | **Per-Tenant Rate Limiting & Autoscaling:** APIM limits each tenant to 1,000 req/min. KEDA horizontally auto-scales ingestion pods up to 20 replicas based on queue depth. |
| **Elevation of Privilege** | Container escape allows unauthorized pod to access host Azure SQL connection string. | AKS Pod Execution Environment | **Workload Identity & Key Vault CSI:** Containers run non-root (`USER 10001`), `readOnlyRootFilesystem: true`, capabilities dropped (`ALL`). Secrets injected in-memory via Secrets Store CSI Driver. |

---

## 6. Technical Safeguards Audit Matrix (§ 164.312)

- **Transmission Security (§ 164.312(e)(1)):** TLS 1.3 enforced externally, TLS 1.2+ internally. Private Endpoints ensure data services are invisible to public internet.
- **Audit Controls (§ 164.312(b)):** Azure Monitor and Log Analytics Workspace configured with 365-day immutable WORM retention. Audit logs record `Timestamp`, `TenantId`, `UserId`, `CorrelationId`, and `Action`.
- **Integrity Controls (§ 164.312(c)(1)):** Cosmos DB session consistency and optimistic concurrency control (`_etag`) prevent phantom overwrites.
- **Access Control (§ 164.312(a)(1)):** Microsoft Entra ID OAuth 2.0 with conditional access and least-privilege Azure RBAC.
