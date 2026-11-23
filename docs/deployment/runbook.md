# Platform Deployment and Troubleshooting Runbook

## 1. Prerequisites & Tooling

Before deploying or running the platform, ensure the following CLI tools are installed:

| Tool | Recommended Version | Verification Command | Purpose |
| :--- | :--- | :--- | :--- |
| **.NET SDK** | 8.0 LTS (`8.0.x`) | `dotnet --version` | Microservices build & testing |
| **Docker Desktop** | 24.0+ | `docker --version` | Container virtualization |
| **Terraform** | 1.5+ | `terraform version` | Azure Infrastructure as Code |
| **Azure CLI** | 2.55+ | `az --version` | Cloud administration & authentication |
| **kubectl** | 1.28+ | `kubectl version --client` | Kubernetes cluster management |
| **Helm** | 3.14+ | `helm version` | Microservice chart packaging |
| **PowerShell** | 7.0+ (or Windows PS 5.1) | `$PSVersionTable.PSVersion` | Tenant onboarding automation |

---

## 2. Local Development Runbook

### 2.1 Starting the Complete Local Environment
Run the automated launch script:
```powershell
.\scripts\run-local.ps1 -Build
```
This boots up:
- **SQL Server 2022** on `localhost:1433` (pre-seeded with MemberDb schema)
- **Azure Service Bus Emulator** on `localhost:5672`
- **Member Service** on `http://localhost:5001`
- **Claims Ingestion Service** on `http://localhost:5002`
- **Prometheus** on `http://localhost:9090`
- **Grafana** on `http://localhost:3000` (credentials: `admin` / `admin`)

### 2.2 Verifying the Local End-to-End Workflow

#### Step 1: Enroll a Member in Tenant-A
```bash
curl -X POST http://localhost:5001/api/members \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: tenant-a" \
  -d '{
    "memberId": "MEM-1001",
    "firstName": "John",
    "lastName": "Doe",
    "dateOfBirth": "1985-06-15",
    "gender": "Male",
    "planCode": "GOLD-PPO",
    "coverageStartDate": "2026-01-01",
    "email": "john.doe@example.com"
  }'
```
*Expected Response:* HTTP 201 Created with JSON member payload and outbox event staged.

#### Step 2: Ingest a Healthcare Claim for Tenant-A
```bash
curl -X POST http://localhost:5002/api/claims \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: tenant-a" \
  -d '{
    "claimId": "claim-1001",
    "tenantId": "tenant-a",
    "memberId": "MEM-1001",
    "providerId": "PRV-9876",
    "claimAmount": 2500,
    "claimStatus": "Submitted",
    "serviceDate": "2026-11-18",
    "diagnosisCodes": ["I10", "E11.9"],
    "procedureCodes": ["99213"]
  }'
```
*Expected Response:* HTTP 201 Created with persisted claim and `ClaimReceived` event emitted.

#### Step 3: Test Cross-Tenant Boundary Enforcement
```bash
# Attempt to query Tenant-A's member while authenticated as Tenant-B
curl -i http://localhost:5001/api/members/MEM-1001 \
  -H "X-Tenant-ID: tenant-b"
```
*Expected Response:* HTTP 404 Not Found (Cross-tenant data leakage is completely blocked by EF Global Query Filter).

---

## 3. Azure Cloud Infrastructure Deployment Runbook

### 3.1 Authentication
```bash
# 1. Log in to Azure CLI
az login

# 2. Select subscription
az account set --subscription "<your-subscription-id>"
```

### 3.2 Provisioning with Terraform
```bash
cd infrastructure/terraform/environments/dev

# Copy example variables
cp terraform.tfvars.example terraform.tfvars
# Update sql_admin_password and other parameters

# Initialize Terraform modules
terraform init

# Review execution plan
terraform plan -out=tfplan

# Apply infrastructure (Requires explicit approval)
terraform apply tfplan
```

---

## 4. Kubernetes Deployment Runbook

### 4.1 Connect to AKS
```bash
az aks get-credentials --resource-group rg-healthcare-platform-dev --name aks-healthcare-platform-dev --overwrite-existing
```

### 4.2 Apply Namespaces & Zero-Trust Network Policies
```bash
kubectl apply -f kubernetes/namespaces/namespaces.yaml
kubectl apply -f kubernetes/network-policies/tenant-network-policies.yaml
```

### 4.3 Deploy Helm Charts per Tenant
```bash
# Deploy Member Service for Tenant A
helm upgrade --install member-service-tenant-a kubernetes/helm/member-service \
  --namespace tenant-a \
  -f kubernetes/helm/values-tenant-a.yaml

# Deploy Member Service for Tenant B
helm upgrade --install member-service-tenant-b kubernetes/helm/member-service \
  --namespace tenant-b \
  -f kubernetes/helm/values-tenant-b.yaml

# Deploy Claims Ingestion Service to platform-shared
helm upgrade --install claims-ingestion kubernetes/helm/claims-ingestion \
  --namespace platform-shared \
  -f kubernetes/helm/claims-ingestion/values.yaml

# Apply KEDA Scaler
kubectl apply -f kubernetes/keda/keda-trigger-auth.yaml
kubectl apply -f kubernetes/keda/claims-ingestion-scaledobject.yaml
```

---

## 5. Automated Tenant Onboarding Runbook

To onboard a new healthcare payer tenant automatically:
```powershell
.\infrastructure\powershell\Onboard-Tenant.ps1 `
  -TenantId "tenant-c" `
  -TenantName "Horizon Health" `
  -Environment "dev" `
  -DatabaseTier "Dedicated" `
  -NotificationEmail "admin@horizonhealth.com"
```

---

## 6. Troubleshooting Matrix

| Issue | Symptom | Diagnostic Step | Remediation |
| :--- | :--- | :--- | :--- |
| **403 Forbidden on API Call** | Client receives `Security Violation: Tenant in header does not match JWT token` | Check `Authorization` Bearer token claims via `jwt.ms` | Ensure the client's OAuth token `tenant_id` claim matches the `X-Tenant-ID` header. APIM anti-spoofing policy rejects mismatches. |
| **Database Connection Failure** | Microservice pod logs `Cannot open database requested by login` | Check Key Vault secret CSI mount: `kubectl exec -it <pod> -n <ns> -- ls /mnt/secrets-store` | Verify Azure SQL firewall rule allows Azure Services and SecretProviderClass has valid Managed Identity Client ID. |
| **Cross-Tenant Network Block** | Pod in `tenant-a` cannot reach pod in `tenant-b` | Check `kubectl logs -n kube-system -l k8s-app=azure-npm` | **Expected Behavior:** NetworkPolicy intentionally isolates tenant namespaces. Services must communicate asynchronously via Azure Service Bus. |
| **KEDA Pods Not Scaling** | Backlog grows in Service Bus topic, but pod count remains at minReplica | Run `kubectl describe scaledobject claims-ingestion-keda-scaler -n platform-shared` | Verify KEDA TriggerAuthentication workload identity has `Azure Service Bus Data Receiver` permissions. |
| **Message Dead-Lettered** | Messages accumulate in `$DeadLetterQueue` of Service Bus subscription | Inspect DLQ message using Service Bus Explorer in Azure Portal | Review `DeadLetterReason` and `DeadLetterErrorDescription`. Malformed JSON Schema payloads or non-existent members are dead-lettered after 5 retries. |
