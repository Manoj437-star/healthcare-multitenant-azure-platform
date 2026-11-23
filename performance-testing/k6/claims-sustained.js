import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { CONFIG, generateSyntheticClaim, buildHeaders } from './config.js';

// Custom Prometheus/k6 Metrics
export const claimsSubmitted = new Counter('claims_submitted_sustained_total');
export const claimsFailed = new Counter('claims_failed_sustained_total');
export const claimsSuccessRate = new Rate('claims_sustained_success_rate');
export const claimLatency = new Trend('claim_sustained_duration_ms', true);
export const throttlingCounter = new Counter('cosmos_429_throttles_total');

/**
 * SCENARIO B: HIGH-THROUGHPUT SUSTAINED WORKLOAD
 * Target: 3,850 claims per minute sustained
 * Calculation: 3,850 / 60 = ~64.17 claims/second (~65 claims/sec)
 * Purpose: Validate sustained high-throughput capacity, KEDA initial scale-out,
 *          Cosmos DB Request Unit (RU) stability, and connection pool behavior.
 */
export const options = {
  scenarios: {
    sustained_high_throughput: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { target: 65, duration: '1m' }, // Ramp-up to 3,850 claims/min (65/s)
        { target: 65, duration: '8m' }, // Sustain 65/s for 8 minutes (~31,200 claims)
        { target: 0, duration: '1m' },  // Graceful ramp-down
      ],
    },
  },
  thresholds: {
    'http_req_duration': ['p(50)<200', 'p(95)<450', 'p(99)<850'],
    'claims_sustained_success_rate': ['rate>0.995'], // 99.5% success
    'http_req_failed': ['rate<0.005'],               // Error rate < 0.5%
    'cosmos_429_throttles_total': ['count<50'],      // Throttling incidents bounded
  },
};

export default function () {
  const tenantId = CONFIG.TENANTS[__VU % CONFIG.TENANTS.length];
  const claimPayload = generateSyntheticClaim(tenantId, __ITER);
  const headers = buildHeaders(tenantId);

  const startTime = Date.now();
  const response = http.post(
    `${CONFIG.BASE_URL}/api/claims`,
    JSON.stringify(claimPayload),
    {
      headers: headers,
      tags: { tenant: tenantId, scenario: 'scenario_b_sustained' }
    }
  );
  const duration = Date.now() - startTime;
  claimLatency.add(duration);

  if (response.status === 429) {
    throttlingCounter.add(1);
    console.warn(`[THROTTLED - HTTP 429] Rate limit or Cosmos RU exceeded for tenant ${tenantId}`);
  }

  const isSuccess = check(response, {
    'status is 201 Created': (r) => r.status === 201,
    'claimId echoed': (r) => {
      try {
        return JSON.parse(r.body).claimId === claimPayload.claimId;
      } catch (e) {
        return false;
      }
    }
  });

  if (isSuccess) {
    claimsSubmitted.add(1);
    claimsSuccessRate.add(1);
  } else {
    claimsFailed.add(1);
    claimsSuccessRate.add(0);
  }
}
