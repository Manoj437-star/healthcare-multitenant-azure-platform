import http from 'k6/http';
import { check } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { CONFIG, generateSyntheticClaim, buildHeaders } from './config.js';

// Custom Prometheus/k6 Metrics
export const peakClaimsSubmitted = new Counter('claims_peak_submitted_total');
export const peakClaimsFailed = new Counter('claims_peak_failed_total');
export const peakClaimsSuccessRate = new Rate('claims_peak_success_rate');
export const peakClaimLatency = new Trend('claim_peak_duration_ms', true);
export const peakThrottlingCounter = new Counter('cosmos_peak_429_throttles_total');

/**
 * SCENARIO C: PEAK BURST WORKLOAD & RECOVERY
 * Target: 15,000 claims per minute burst
 * Calculation: 15,000 / 60 = 250 claims/second
 * Purpose: Stress test burst ingestion handling, KEDA explosive scale-out to maximum replicas (20),
 *          Service Bus queue backlog absorption, Cosmos DB autoscale RU limits, and recovery dynamics.
 */
export const options = {
  scenarios: {
    peak_burst_and_recovery: {
      executor: 'ramping-arrival-rate',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 600,
      stages: [
        { target: 65, duration: '1m' },   // Stage 1: Warm-up to sustained volume (3,850/min)
        { target: 250, duration: '45s' }, // Stage 2: Explosive ramp to 15,000/min (250/s)
        { target: 250, duration: '3m' },  // Stage 3: Sustain peak burst for 3 mins (~45,000 claims)
        { target: 65, duration: '1m' },   // Stage 4: De-escalate to sustained volume
        { target: 10, duration: '2m' },   // Stage 5: Cooldown to observe KEDA scale-in & queue drainage
      ],
    },
  },
  thresholds: {
    'http_req_duration': ['p(50)<300', 'p(95)<750', 'p(99)<1500'], // Peak SLAs
    'claims_peak_success_rate': ['rate>0.985'],                     // 98.5% success during severe burst
    'http_req_failed': ['rate<0.015'],                              // Maximum 1.5% errors under peak
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
      tags: { tenant: tenantId, scenario: 'scenario_c_peak' }
    }
  );
  const duration = Date.now() - startTime;
  peakClaimLatency.add(duration);

  if (response.status === 429) {
    peakThrottlingCounter.add(1);
    console.warn(`[PEAK 429] Rate limit reached under burst for tenant ${tenantId}`);
  }

  const isSuccess = check(response, {
    'status is 201 Created': (r) => r.status === 201,
    'claimId valid': (r) => {
      try {
        return JSON.parse(r.body).claimId === claimPayload.claimId;
      } catch (e) {
        return false;
      }
    }
  });

  if (isSuccess) {
    peakClaimsSubmitted.add(1);
    peakClaimsSuccessRate.add(1);
  } else {
    peakClaimsFailed.add(1);
    peakClaimsSuccessRate.add(0);
  }
}
