# Security Limitations & Enterprise Boundary Specifications

## 1. Executive Purpose

In accordance with enterprise risk governance, this document explicitly articulates the **technical and operational boundaries, assumptions, and known limitations** of the current Proof of Concept (POC) implementation.

> [!CAUTION]
> **Regulatory Disclaimer:** Technical controls alone do not constitute HIPAA certification. Full HIPAA compliance requires non-technical administrative controls, workforce compliance training, executed Business Associate Agreements (BAAs), physical facility controls, and periodic audits.

---

## 2. Technical Limitations & Boundaries

### 2.1 Single Physical Partition Key in Cosmos DB
- **Current State:** The claims document container is partitioned solely by `/tenantId`.
- **Limitation:** A physical Cosmos DB partition is capped at 50 GB and 10,000 RU/s. If a single Tier 1 enterprise payer bursts beyond 1,700 writes/sec, partition-level 429 throttling will occur regardless of overall account RU capacity.
- **Production Roadmap:** Implement hierarchical partition keys `["/tenantId", "/serviceDate"]` or synthetic composite keys (`tenantId_yearMonth`) prior to high-volume production go-live.

### 2.2 Serverless SQL Engine RLS Constraints
- **Current State:** Synapse Serverless SQL Pools do not support native `CREATE SECURITY POLICY` or user-defined inline functions (`CREATE FUNCTION`).
- **Limitation:** Tenant isolation in Serverless SQL relies on view-level `SESSION_CONTEXT(N'TenantId')` or dedicated tenant views (`TenantAClaimsView`). A direct query against the base Cosmos DB store by an administrator bypasses view filtering.
- **Production Roadmap:** For organizations requiring strict engine-enforced cryptographic security policies, deploy Azure Synapse Dedicated SQL Pools or Azure SQL Database for analytical read models.

### 2.3 Secrets Store CSI Driver In-Memory Mounting
- **Current State:** Secrets are synchronized from Azure Key Vault to Kubernetes in-memory volume mounts (`tmpfs`).
- **Limitation:** While secrets are never written to disk or etcd, a container breach with root capabilities could theoretically read the mounted filesystem.
- **Mitigation Implemented:** All microservice containers run strictly as non-root (`USER 10001`), with `readOnlyRootFilesystem: true`, and all Linux capabilities dropped (`ALL`).

### 2.4 Token Expiration & Continuous Access Evaluation (CAE)
- **Current State:** Microservices validate JWT signature, expiration, issuer, and claims locally via public JWKS endpoints.
- **Limitation:** If a user or service principal credential is revoked in Microsoft Entra ID, the existing token remains valid until its expiration timestamp (`exp`).
- **Production Roadmap:** Enable Microsoft Entra Continuous Access Evaluation (CAE) to trigger real-time revocation events.
