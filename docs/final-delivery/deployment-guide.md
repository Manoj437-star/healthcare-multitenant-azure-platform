# Platform Deployment & Operational Runbook

## 1. Quickstart: Local Development Environment

The entire microservices platform is containerized and runnable locally using Docker Compose without requiring live Azure credentials:

```powershell
# Boot up SQL Server 2022, Service Bus Emulator, Member Service, Claims Ingestion, Prometheus, and Grafana:
.\scripts\run-local.ps1 -Build
```

### Local Endpoints:
- **Member Service Swagger:** [http://localhost:5001/swagger](http://localhost:5001/swagger)
- **Claims Ingestion Swagger:** [http://localhost:5002/swagger](http://localhost:5002/swagger)
- **Grafana Dashboard:** [http://localhost:3000](http://localhost:3000) (User: `admin`, Password: `HealthcareSecureGrafana2026!`)
- **Prometheus UI:** [http://localhost:9090](http://localhost:9090)
- **SQL Server 2022:** `localhost:1433` (`sa` / `HealthcareStrongP@ss2026!`)

---

## 2. Azure Infrastructure Deployment via Terraform

### Step 1: Authentication & Subscription Selection
```bash
az login
az account set --subscription "<your-subscription-id>"
```

### Step 2: Initialize & Apply Terraform Dev Environment
```bash
cd infrastructure/terraform/environments/dev

# Copy and customize variables:
cp terraform.tfvars.example terraform.tfvars

# Initialize Terraform modules:
terraform init

# Review execution plan:
terraform plan -out=tfplan

# Apply infrastructure (Requires explicit approval):
terraform apply tfplan
```

---

## 3. Kubernetes & Helm Deployment

### Step 1: Connect to AKS Cluster
```bash
az aks get-credentials --resource-group rg-healthcare-platform-dev --name aks-healthcare-platform-dev --overwrite-existing
```

### Step 2: Apply Namespaces & NetworkPolicies
```bash
kubectl apply -f kubernetes/namespaces/namespaces.yaml
kubectl apply -f kubernetes/network-policies/tenant-network-policies.yaml
```

### Step 3: Deploy Helm Charts per Tenant
```bash
# Deploy Member Service to Tenant A (Dedicated Enterprise Values)
helm upgrade --install member-service-tenant-a kubernetes/helm/member-service \
  --namespace tenant-a \
  -f kubernetes/helm/values-tenant-a.yaml

# Deploy Member Service to Tenant B (Standard Values)
helm upgrade --install member-service-tenant-b kubernetes/helm/member-service \
  --namespace tenant-b \
  -f kubernetes/helm/values-tenant-b.yaml

# Deploy Claims Ingestion Service to platform-shared
helm upgrade --install claims-ingestion kubernetes/helm/claims-ingestion \
  --namespace platform-shared \
  -f kubernetes/helm/claims-ingestion/values.yaml

# Apply KEDA Scaler & Workload Identity
kubectl apply -f kubernetes/keda/keda-trigger-auth.yaml
kubectl apply -f kubernetes/keda/claims-ingestion-scaledobject.yaml
```

---

## 4. Automated Tenant Onboarding Automation

To provision an independent tenant into the running cluster with complete compute, database, and network isolation:

```powershell
.\infrastructure\powershell\Onboard-Tenant.ps1 `
  -TenantId "tenant-c" `
  -TenantName "Horizon Health Payer" `
  -Environment "dev" `
  -DatabaseTier "Dedicated" `
  -NotificationEmail "admin@horizonhealth.com"
```
The script runs in 9 automated phases creating the namespace, applying NetworkPolicies, configuring database schemas, creating Key Vault secrets, issuing APIM subscriptions, registering Entra ID apps, and generating an onboarding summary package.
