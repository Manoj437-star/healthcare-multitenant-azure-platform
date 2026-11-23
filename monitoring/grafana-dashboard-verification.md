# Grafana Monitoring & Dashboard Verification Report

## 1. Executive Summary

This report documents the operational verification of Grafana monitoring and Prometheus telemetry for the **Healthcare Multi-Tenant Microservices Platform**, specifically focusing on the **Claims Ingestion Performance & KEDA Scaler Dashboard** (`claims-load-testing`).

---

## 2. Configuration & Connectivity Verification

| Component | Target URL | Configuration File | Status | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Grafana Instance** | `http://localhost:3000` | `docker/docker-compose.yml` | **Configured** | Version: Grafana 11.2.0 |
| **Prometheus Datasource** | `http://prometheus:9090` | `monitoring/grafana/provisioning/datasources/datasource.yml` | **Configured** | Default datasource (Proxy mode) |
| **Dashboard Provisioning** | `/var/lib/grafana/dashboards` | `monitoring/grafana/provisioning/dashboards/dashboard.yml` | **Configured** | Auto-loads on startup |
| **Dashboard UID** | `claims-load-testing` | `monitoring/dashboards/claims-load-testing.json` | **Validated** | 10 active panels, valid JSON |

---

## 3. Panel Inspection & Metrics Coverage

The dashboard `claims-load-testing.json` was verified against all 10 required operational dimensions:

1. **Claims Ingestion Request Rate:**
   - Panel: *Requests per Second (by Tenant)*
   - Query: `sum by (tenant_id) (rate(http_requests_received_total{service="claims-ingestion"}[15s]))`
   - Unit: Requests/sec (`reqps`).
2. **Claims Processed per Minute:**
   - Panel: *Claims Processed per Minute*
   - Query: `sum(rate(claims_submitted_total[1m])) * 60`
3. **Ingestion Latency SLAs (p50, p95, p99):**
   - Panel: *Ingestion Latency Percentiles (p50, p95, p99)*
   - Queries: `histogram_quantile(0.50|0.95|0.99, sum by (le) (rate(http_request_duration_seconds_bucket...))) * 1000`
   - Unit: Milliseconds (`ms`).
4. **Claims Processing Errors (HTTP 4xx & 5xx):**
   - Panel: *HTTP Error Statuses (4xx & 5xx)*
   - Query: `sum by (status_code) (rate(http_requests_received_total{status_code=~"4..|5.."}[15s]))`
5. **Service Bus Active Backlog vs Dead-Letter Count:**
   - Panel: *Service Bus Active Backlog vs Dead-Letter Count*
   - Queries: `azure_servicebus_subscription_active_messages` and `azure_servicebus_subscription_deadletter_messages`
6. **KEDA Active Pod Replicas:**
   - Panel: *KEDA Active Pod Replicas (Scale-Out / Scale-In)*
   - Queries: `kube_deployment_status_replicas` and `keda_scaler_active`
7. **Ingestion Pod CPU Utilization (%):**
   - Panel: *Ingestion Pod CPU Utilization (%)*
   - Query: `sum(rate(container_cpu_usage_seconds_total...)) / sum(kube_pod_container_resource_limits...) * 100`
8. **Ingestion Pod Memory Working Set (%):**
   - Panel: *Ingestion Pod Memory Working Set (%)*
   - Query: `sum(container_memory_working_set_bytes...) / sum(kube_pod_container_resource_limits...) * 100`
9. **Cosmos DB Consumed Request Units (RU):**
   - Panel: *Cosmos DB Consumed RUs / sec*
   - Query: `sum(rate(azure_cosmosdb_total_request_units[1m]))`
10. **Cosmos DB Throttling Incidents (HTTP 429):**
    - Panel: *Cosmos DB Throttling Incidents (HTTP 429)*
    - Query: `sum(rate(azure_cosmosdb_requests_total{status_code="429"}[1m]))`

---

## 4. Telemetry Source Assessment: Actual vs Simulated Metrics

- **Local Development / Docker Compose:**
  Microservices expose actual Prometheus metrics via `/metrics` endpoint. In the local environment, Prometheus scrapes `member-service:8080` and `claims-ingestion-service:8080`.
- **Azure Cloud Telemetry:**
  In AKS and Azure Monitor, metrics are collected via Azure Monitor Container Insights and Prometheus metrics addon, with Service Bus metrics exported via Azure Monitor Prometheus Scraper.
- **Cosmos DB RU Telemetry:**
  In local simulation mode, Cosmos DB RU metrics are zero or tracked in-memory. In Azure Cloud, `azure_cosmosdb_total_request_units` is exported directly via the Azure Monitor Prometheus collector.

---

## 5. Security Hardening: Credential Remediation

> [!CAUTION]
> Default credentials (`admin`/`admin`) pose a severe HIPAA audit vulnerability and must never be permitted in production or shared development environments.

### Remediation Applied:
- Updated `docker/docker-compose.yml` to parameterize admin credentials using environment variables:
  ```yaml
  GF_SECURITY_ADMIN_USER=${GRAFANA_ADMIN_USER:-admin}
  GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD:-HealthcareSecureGrafana2026!}
  ```
- Created `docker/.env.example` documenting the credential overrides.
- In production Kubernetes deployments, Grafana admin credentials must be sourced from Azure Key Vault via SecretProviderClass or Kubernetes Secret (`envFrom.secretRef`).
