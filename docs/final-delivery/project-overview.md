# Project Overview – Azure Multi-Tenant Healthcare Microservices Platform

## 1. Executive Mission & Business Scope

The **Azure Multi-Tenant Healthcare Microservices Platform** is an enterprise-grade cloud-native backplane engineered to support **50+ payer organizations (tenants)** processing up to **10 million claims per month**.

The healthcare payer value chain encompasses:
1. **Member Enrollment & Eligibility Management:** Master policy registration, coverage dates, benefits accumulators, and subscriber tracking.
2. **Claims Ingestion & Schema Validation:** High-throughput EDI/REST claim intake adhering to ANSI ASC X12 837 claim transaction standards.
3. **Claims Adjudication Engine (Architectural Design):** Medical necessity determination, co-pay/deductible application, and benefit limit calculation via CQRS and Event Sourcing.
4. **Provider Directory Service (Design):** National Provider Identifier (NPI) registry and network contract validation.
5. **Care Management Service (Design):** High-risk member outreach and clinical management triggers.
6. **Enterprise Notifications (Design):** White-labeled Explanation of Benefits (EOB) and remittance notifications.
7. **Zero-Trust API Gateway:** Azure API Management (APIM) enforcing OAuth 2.0 / JWT validation, tenant claim extraction, anti-spoofing verification, and distributed rate limiting (1,000 req/min/tenant).

---

## 2. Technology Stack & Component Justifications

| Domain | Selected Technology | Version / Tier | Architectural Justification |
| :--- | :--- | :--- | :--- |
| **Compute** | Azure Kubernetes Service (AKS) | Kubernetes 1.29+ | High-density container orchestration, Workload Identity, Azure CNI, and automated node pool autoscaling. |
| **Application** | .NET LTS / ASP.NET Core | .NET 8.0 LTS, C# 12 | Enterprise-grade type safety, high I/O throughput, native Linux container support, and asynchronous non-blocking runtime. |
| **Relational Data**| Azure SQL Database | General Purpose (S0 dev) | Dedicated database per Tier 1 enterprise tenant (`tenant-a-db`); shared pooled database (`shared-payers-db`) with Row-Level Security for Tier 2 standard payers. |
| **Document Store** | Azure Cosmos DB | SQL API (Autoscale RUs) | Sub-10ms point writes for claims ingestion, partitioned by `/tenantId`, with integrated analytical store (Synapse Link). |
| **Messaging** | Azure Service Bus | Premium (AMQP 1.0) | Dedicated messaging units, enterprise topic/subscription routing rules, dead-letter queues, and deterministic FIFO processing. |
| **API Gateway** | Azure API Management | Developer / Premium | Centralized TLS 1.3 termination, Entra ID JWT validation, anti-spoofing header verification, and per-tenant rate limiting. |
| **Secrets** | Azure Key Vault | Standard / Premium | HSM-backed secret storage with soft-delete, purge protection, and automated in-memory secret rotation via Secrets Store CSI Driver. |
| **Autoscaling** | KEDA | v2.14+ | Event-driven pod autoscaling based on Service Bus queue lag (scaling from 2 to 20 replicas) bypassing CPU metric lag. |
| **Analytics** | Azure Synapse Analytics | Serverless SQL Pool | Zero-ETL HTAP queries over Cosmos DB Analytical Store with zero operational RU consumption. |
