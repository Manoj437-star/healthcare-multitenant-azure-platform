# Healthcare Platform Performance Testing Suite

## 1. Overview

This directory contains automated, distributed load testing suites built with **k6** (and a **Locust** alternative) to validate the Claims Ingestion platform against three distinct production workload scenarios:

- **Scenario A (Monthly Baseline):** 10 Million claims/month (~231 claims/minute).
- **Scenario B (High-Throughput Sustained):** 3,850 claims/minute (~64 claims/second).
- **Scenario C (Peak Workload):** 15,000 claims/minute burst (~250 claims/second).

---

## 2. Prerequisites & Tool Installation

### Install k6:
- **Windows (winget):** `winget install k6 --source winget`
- **Windows (choco):** `choco install k6`
- **Linux/macOS:** `brew install k6` or `sudo apt install k6`
- **Docker:** `docker run -i grafana/k6 run - <performance-testing/k6/claims-ingestion.js`

### Install Locust (Alternative):
```bash
pip install locust
```

---

## 3. Running the Test Scenarios

### Scenario A: Monthly Business Volume (10M claims/month baseline)
```bash
# Target local docker-compose environment:
k6 run performance-testing/k6/claims-ingestion.js

# Target Azure APIM Ingress Gateway:
TARGET_URL="https://apim-healthcare-dev-01.azure-api.net" k6 run performance-testing/k6/claims-ingestion.js
```

### Scenario B: High-Throughput Workload (3,850 claims/min sustained)
```bash
k6 run performance-testing/k6/claims-sustained.js
```

### Scenario C: Peak Burst Workload (15,000 claims/min burst & recovery)
```bash
k6 run performance-testing/k6/claims-peak.js
```

### Running Locust (Web UI Mode)
```bash
locust -f performance-testing/locust/locustfile.py --host http://localhost:5002
```
Open [http://localhost:8089](http://localhost:8089) in your browser to configure virtual users and spawn rate.

---

## 4. Real-Time Observability during Load Tests

While running any scenario, open the pre-configured Grafana dashboard:
- URL: [http://localhost:3000](http://localhost:3000) (User: `admin`, Pass: `admin`)
- Dashboard: **Claims Ingestion Performance & KEDA Scaler Dashboard** (`claims-load-testing`)
- Panels display real-time throughput, p50/p95/p99 latency, KEDA pod scaling count, and Cosmos DB RU utilization.
