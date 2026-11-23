# Azure API Management (APIM) Topology & Security Architecture

## 1. Gateway Deployment Topology

Azure API Management serves as the zero-trust front door for all external payer interactions, provider EHR portals, and EDI clearinghouses.

```mermaid
graph TD
    Client["Healthcare Client / Payer / Provider"]

    subgraph AzureCloud ["Microsoft Azure Cloud"]
        subgraph VNet ["Virtual Network (hub-spoke-vnet)"]
            subgraph SubnetAPIM ["APIM Subnet (Internal Mode / VNet Integrated)"]
                APIM["Azure API Management (Developer / Premium Tier)
                - Inbound Policy Engine
                - OAuth 2.0 / JWT Enforcement
                - Tenant Rate Limiter
                - Dynamic Backend Router"]
            end

            subgraph SubnetAKS ["AKS Pod Subnet (k8s-pod-subnet)"]
                subgraph NSTenantA ["Namespace: tenant-a"]
                    MS_A["Member Service Pods (tenant-a)"]
                end
                subgraph NSTenantB ["Namespace: tenant-b"]
                    MS_B["Member Service Pods (tenant-b)"]
                end
                subgraph NSShared ["Namespace: platform-shared"]
                    CI["Claims Ingestion Service Pods"]
                end
            end
        end

        EntraID["Microsoft Entra ID (Azure AD)
        - Multi-Tenant App Registrations
        - Client Credentials / Authorization Code"]
        AppInsights["Application Insights
        - Request/Response Telemetry
        - W3C Traceparent Logging"]
        KeyVault["Azure Key Vault
        - Gateway TLS Certificates
        - JWT Signing Keys"]
    end

    Client -->|HTTPS (TLS 1.3)| APIM
    Client -.->|Acquire Token| EntraID
    APIM -->|Validate Signature| EntraID
    APIM -->|Read TLS Cert| KeyVault
    APIM -->|W3C Traces & Logs| AppInsights
    APIM -->|Internal Cluster DNS: member-service.tenant-a.svc| MS_A
    APIM -->|Internal Cluster DNS: member-service.tenant-b.svc| MS_B
    APIM -->|Internal Cluster DNS: claims-ingestion.platform-shared.svc| CI
```

---

## 2. Inbound Policy Execution Pipeline

To safeguard against tenant impersonation and denial-of-service, APIM executes a strict pipeline:

```
[Inbound Request]
       │
       ▼
1. Validate JWT Token
   - Check Issuer: https://login.microsoftonline.com/{tenant_issuer}/v2.0
   - Check Audience: api://healthcare-platform
   - Check Expiration: exp > now()
       │
       ▼
2. Extract Tenant Context
   - Extract 'tid' (Tenant Directory ID) or custom claim 'tenant_id'
   - Store in context variable: context.Variables["tenantId"]
       │
       ▼
3. Validate Client Header vs Token Claims (Anti-Spoofing)
   - Inspect incoming 'X-Tenant-ID' header
   - IF (Header != null AND Header != context.Variables["tenantId"])
     THEN Return 403 Forbidden ("Tenant header does not match authenticated token claim.")
   - Enforce downstream header: set-header "X-Tenant-ID" = context.Variables["tenantId"]
       │
       ▼
4. Distributed Rate Limiting (Per Tenant)
   - rate-limit-by-key: 1000 calls / 60 seconds
   - Counter Key: context.Variables["tenantId"]
   - If exceeded: Return 429 Too Many Requests (with Retry-After header)
       │
       ▼
5. Correlation & Trace Context
   - If 'X-Correlation-ID' header is missing, generate new GUID
   - Inject into backend request and response headers
       │
       ▼
6. Dynamic Backend Routing
   - Route dynamically based on tenant namespace:
     http://member-service.{tenantId}.svc.cluster.local:8080/api/members
       │
       ▼
[Forward to Backend Pod]
```

---

## 3. Anti-Spoofing Rule Specification

> [!CAUTION]
> **Zero Trust Rule:** Never trust an unverified `X-Tenant-ID` header supplied by an external client. The system must verify that the authenticated identity is authorized for that specific tenant. If an external client supplies `X-Tenant-ID: tenant-b` but their JWT belongs to `tenant-a`, the APIM gateway rejects the request immediately with HTTP 403 before it ever reaches internal microservices.
