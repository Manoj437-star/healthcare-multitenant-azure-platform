import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { CONFIG, generateSyntheticClaim, buildHeaders } from './config.js';

// Custom Prometheus/k6 Metrics
export const claimsSubmitted = new Counter('claims_submitted_total');
export const claimsFailed = new Counter('claims_failed_total');
export const claimsRate = new Rate('claims_success_rate');
export const claimLatency = new Trend('claim_processing_duration_ms', true);

/**
 * SCENARIO A: MONTHLY BUSINESS VOLUME BASELINE
 * Target: 10 Million claims / 30-day month
 * Calculation: 10,000,000 / (30 * 24 * 60) = ~231.5 claims/minute = ~3.86 claims/second
 * Purpose: Validate sustained baseline processing stability, memory leak freedom, and SLA conformance.
 */
export const options = {
  scenarios: {
    monthly_baseline_sustained: {
      executor: 'constant-arrival-rate',
      rate: 4,               // ~240 claims per minute (matches ~10M claims/month)
      timeUnit: '1s',
      duration: '5m',        // 5-minute sustained validation run
      preAllocatedVUs: 10,
      maxVUs: 25,
    },
  },
  thresholds: {
    'http_req_duration': ['p(50)<150', 'p(95)<300', 'p(99)<500'], // Latency SLAs (ms)
    'claims_success_rate': ['rate>0.999'],                         // 99.9% success rate
    'http_req_failed': ['rate<0.001'],                             // Error rate < 0.1%
  },
};

export default function (data) {
  // Distribute load between tenant-a (Tier 1) and tenant-b (Tier 2)
  const tenantId = CONFIG.TENANTS[__VU % CONFIG.TENANTS.length];
  const claimPayload = generateSyntheticClaim(tenantId, __ITER);
  const headers = buildHeaders(tenantId);

  const startTime = Date.now();
  const response = http.post(
    `${CONFIG.BASE_URL}/api/claims`,
    JSON.stringify(claimPayload),
    { headers: headers, tags: { tenant: tenantId, scenario: 'scenario_a_baseline' } }
  );
  const duration = Date.now() - startTime;
  claimLatency.add(duration);

  const isSuccess = check(response, {
    'status is 201 Created': (r) => r.status === 201,
    'claimId echoed in response': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.claimId === claimPayload.claimId;
      } catch (e) {
        return false;
      }
    },
    'tenantId matches payload': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.tenantId === tenantId;
      } catch (e) {
        return false;
      }
    }
  });

  if (isSuccess) {
    claimsSubmitted.add(1);
    claimsRate.add(1);
  } else {
    claimsFailed.add(1);
    claimsRate.add(0);
    console.error(`[SCENARIO A ERROR] Status: ${response.status}, Body: ${response.body}, Tenant: ${tenantId}`);
  }

  // Small pacing sleep between iterations
  sleep(0.1);
}
