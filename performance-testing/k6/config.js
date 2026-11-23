/**
 * Common configuration and synthetic data generation utilities for k6 load testing.
 * Complies with strict healthcare claim JSON schema requirements.
 * STRICT HIPAA RULE: Generates only synthetic data.
 */

// Safe environment variable resolution for k6 and Node test runners
const targetUrl = (typeof __ENV !== 'undefined' && __ENV.TARGET_URL) ||
                  (typeof process !== 'undefined' && process.env && process.env.TARGET_URL) ||
                  'http://localhost:5002';

export const CONFIG = {
  // Target API Gateway or direct service endpoint
  BASE_URL: targetUrl,

  // Tenants participating in the load test
  TENANTS: ['tenant-a', 'tenant-b'],

  // Synthetic medical reference datasets (ICD-10-CM and CPT-4)
  DIAGNOSIS_CODES: [
    'I10',      // Essential hypertension
    'E11.9',    // Type 2 diabetes without complications
    'Z00.00',   // General adult medical examination
    'J45.909',  // Unspecified asthma
    'M54.5',    // Low back pain
    'K21.9'     // Gastro-esophageal reflux disease
  ],

  PROCEDURE_CODES: [
    '99213',    // Office visit, established patient, low-moderate complexity
    '99214',    // Office visit, established patient, moderate-high complexity
    '80053',    // Comprehensive metabolic panel
    '85025',    // Complete blood count (CBC)
    '93000',    // Electrocardiogram (ECG)
    '71045'     // Chest X-ray, single view
  ]
};

// Cross-runtime base64 helper
function base64Encode(str) {
  if (typeof btoa !== 'undefined') {
    return btoa(str);
  }
  if (typeof Buffer !== 'undefined') {
    return Buffer.from(str).toString('base64');
  }
  return str;
}

/**
 * Generates a mock signed JWT token containing tenant claims for authentication.
 * Used when testing through APIM or authenticated microservice endpoints.
 */
export function getMockJwt(tenantId) {
  const header = base64Encode(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const now = Math.floor(Date.now() / 1000);
  const payload = base64Encode(JSON.stringify({
    iss: 'https://login.microsoftonline.com/test-tenant/v2.0',
    aud: 'api://healthcare-multitenant-platform',
    sub: `user-${tenantId}-loadtest`,
    tenant_id: tenantId,
    tid: tenantId,
    nbf: now - 60,
    exp: now + 3600,
    iat: now
  }));
  const signature = base64Encode('mock-signature-for-loadtest-environment');
  return `${header}.${payload}.${signature}`;
}

/**
 * Generates a unique, valid synthetic healthcare claim payload.
 */
export function generateSyntheticClaim(tenantId, index) {
  const timestamp = Date.now();
  const randomSuffix = Math.floor(Math.random() * 1000000);
  const claimId = `CLM-${tenantId}-${timestamp}-${index}-${randomSuffix}`;
  const memberNumber = Math.floor(Math.random() * 50000) + 10000;
  const providerNumber = Math.floor(Math.random() * 5000) + 1000;

  // Random amount between $45.00 and $4,500.00
  const claimAmount = Math.round((45.0 + Math.random() * 4455.0) * 100) / 100;

  // Select 1-2 diagnosis codes and 1-2 procedure codes
  const diag1 = CONFIG.DIAGNOSIS_CODES[Math.floor(Math.random() * CONFIG.DIAGNOSIS_CODES.length)];
  const diag2 = CONFIG.DIAGNOSIS_CODES[Math.floor(Math.random() * CONFIG.DIAGNOSIS_CODES.length)];
  const proc1 = CONFIG.PROCEDURE_CODES[Math.floor(Math.random() * CONFIG.PROCEDURE_CODES.length)];

  // Service date: between 1 and 30 days ago
  const daysAgo = Math.floor(Math.random() * 30) + 1;
  const serviceDate = new Date(Date.now() - daysAgo * 24 * 60 * 60 * 1000).toISOString().split('T')[0];

  return {
    claimId: claimId,
    tenantId: tenantId,
    memberId: `MEM-${memberNumber}`,
    providerId: `PRV-${providerNumber}`,
    claimAmount: claimAmount,
    claimStatus: 'Submitted',
    serviceDate: serviceDate,
    diagnosisCodes: [diag1, diag2],
    procedureCodes: [proc1]
  };
}

/**
 * Builds HTTP headers including anti-spoofing tenant headers and correlation IDs.
 */
export function buildHeaders(tenantId) {
  const correlationId = `k6-corr-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
  const token = getMockJwt(tenantId);

  return {
    'Content-Type': 'application/json',
    'X-Tenant-ID': tenantId,
    'X-Correlation-ID': correlationId,
    'Authorization': `Bearer ${token}`
  };
}
