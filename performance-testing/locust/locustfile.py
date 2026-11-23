"""
Locust Performance Testing Suite for Healthcare Multi-Tenant Platform
Alternative to k6 for distributed Python-based load testing.
"""

import time
import random
import uuid
from datetime import datetime, timedelta
from locust import HttpUser, task, between, events

TENANTS = ["tenant-a", "tenant-b"]
DIAGNOSIS_CODES = ["I10", "E11.9", "Z00.00", "J45.909", "M54.5", "K21.9"]
PROCEDURE_CODES = ["99213", "99214", "80053", "85025", "93000", "71045"]

class HealthcareClaimsUser(HttpUser):
    # Wait between 0.1 and 0.5 seconds between tasks to simulate fast batch ingestion
    wait_time = between(0.1, 0.5)

    def on_start(self):
        """Pick a tenant identity per virtual user session"""
        self.tenant_id = random.choice(TENANTS)

    def _generate_synthetic_claim(self):
        timestamp = int(time.time() * 1000)
        random_suffix = random.randint(100000, 999999)
        claim_id = f"CLM-{self.tenant_id}-{timestamp}-{random_suffix}"
        service_date = (datetime.utcnow() - timedelta(days=random.randint(1, 30))).strftime("%Y-%m-%d")

        return {
            "claimId": claim_id,
            "tenantId": self.tenant_id,
            "memberId": f"MEM-{random.randint(10000, 99999)}",
            "providerId": f"PRV-{random.randint(1000, 9999)}",
            "claimAmount": round(random.uniform(45.0, 4500.0), 2),
            "claimStatus": "Submitted",
            "serviceDate": service_date,
            "diagnosisCodes": random.sample(DIAGNOSIS_CODES, 2),
            "procedureCodes": random.sample(PROCEDURE_CODES, 1)
        }

    @task(10)
    def submit_claim(self):
        """Submit synthetic healthcare claim to Claims Ingestion API"""
        payload = self._generate_synthetic_claim()
        correlation_id = f"locust-corr-{uuid.uuid4()}"

        headers = {
            "Content-Type": "application/json",
            "X-Tenant-ID": self.tenant_id,
            "X-Correlation-ID": correlation_id
        }

        with self.client.post("/api/claims", json=payload, headers=headers, catch_response=True) as response:
            if response.status_code == 201:
                response.success()
            elif response.status_code == 429:
                response.failure("Rate limited / Cosmos RU Throttled (HTTP 429)")
            else:
                response.failure(f"Unexpected status: {response.status_code} - {response.text}")

    @task(1)
    def check_health(self):
        """Periodic liveness probe verification"""
        self.client.get("/health")
