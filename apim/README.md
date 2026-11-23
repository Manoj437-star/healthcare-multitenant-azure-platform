# Azure API Management (APIM) Configuration & Deployment Guide

## 1. Overview

Azure API Management serves as the zero-trust ingress gateway for the Healthcare Multi-Tenant Microservices Platform. It enforces:
- Microsoft Entra ID OAuth 2.0 / JWT signature and audience validation.
- Tenant claim extraction (`tenant_id` or `tid`).
- Anti-spoofing validation: verifies that client-supplied `X-Tenant-ID` matches authenticated JWT claims.
- Per-tenant rate limiting: 1,000 requests/minute per tenant.
- Dynamic backend routing to dedicated tenant Kubernetes namespaces.
- W3C distributed tracing and correlation ID propagation.

---

## 2. API Definitions

### 2.1 Member Service API
- **Display Name:** Healthcare Member Service API
- **API Path:** `/api/members`
- **Backend Service URL:** `http://member-service.platform-shared.svc.cluster.local:8080` (overridden dynamically by policy)
- **Policy File:** `apim/policies/member-service-policy.xml`

### 2.2 Claims Ingestion Service API
- **Display Name:** Healthcare Claims Ingestion API
- **API Path:** `/api/claims`
- **Backend Service URL:** `http://claims-ingestion.platform-shared.svc.cluster.local:8080`
- **Policy File:** `apim/policies/claims-ingestion-policy.xml`

---

## 3. Deployment Instructions via Azure CLI

To apply the APIM XML policies to an existing Azure API Management instance:

```bash
# 1. Define Variables
RESOURCE_GROUP="rg-healthcare-platform-dev"
APIM_NAME="apim-healthcare-platform-dev"

# 2. Apply Global Policy
az apim policy update \
  --resource-group "$RESOURCE_GROUP" \
  --service-name "$APIM_NAME" \
  --policy-format xml \
  --value @apim/policies/global-policy.xml

# 3. Create or Update Member Service API
az apim api create \
  --resource-group "$RESOURCE_GROUP" \
  --service-name "$APIM_NAME" \
  --api-id "member-service-api" \
  --path "/api/members" \
  --display-name "Healthcare Member Service API" \
  --protocols https

# 4. Apply Policy to Member Service API
az apim api policy update \
  --resource-group "$RESOURCE_GROUP" \
  --service-name "$APIM_NAME" \
  --api-id "member-service-api" \
  --policy-format xml \
  --value @apim/policies/member-service-policy.xml

# 5. Create or Update Claims Ingestion API
az apim api create \
  --resource-group "$RESOURCE_GROUP" \
  --service-name "$APIM_NAME" \
  --api-id "claims-ingestion-api" \
  --path "/api/claims" \
  --display-name "Healthcare Claims Ingestion API" \
  --protocols https

# 6. Apply Policy to Claims Ingestion API
az apim api policy update \
  --resource-group "$RESOURCE_GROUP" \
  --service-name "$APIM_NAME" \
  --api-id "claims-ingestion-api" \
  --policy-format xml \
  --value @apim/policies/claims-ingestion-policy.xml
```
