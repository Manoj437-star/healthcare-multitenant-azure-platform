-- ==============================================================================
-- STEP 2: CREATE REPORTING VIEWS OVER COSMOS DB ANALYTICAL STORE
-- Uses Synapse Link OPENROWSET with automatic columnar projection
-- Zero transactional RU consumption!
-- ==============================================================================

USE HealthcareAnalyticsDb;
GO

CREATE OR ALTER VIEW analytics.ClaimsReportingView
AS
SELECT
    -- Tenant & Identity Attributes
    r.tenantId AS TenantId,
    r.claimId AS ClaimId,
    r.providerId AS ProviderId,

    -- De-identified Member Reference (PHI Protection)
    CONCAT('HASH-', SUBSTRING(sys.fn_sqlvarbasetostr(HASHBYTES('SHA2_256', r.memberId)), 3, 12)) AS AnonymizedMemberKey,

    -- Financial & Operational Metrics
    CAST(r.claimAmount AS DECIMAL(18, 2)) AS ClaimAmount,
    r.currency AS Currency,
    r.claimStatus AS ClaimStatus,
    CAST(r.serviceDate AS DATE) AS ServiceDate,
    CAST(r.submittedAtUtc AS DATETIME2) AS SubmittedAtUtc,

    -- Time Dimensions for Fast Aggregations
    YEAR(CAST(r.submittedAtUtc AS DATETIME2)) AS SubmissionYear,
    MONTH(CAST(r.submittedAtUtc AS DATETIME2)) AS SubmissionMonth,
    DATEFROMPARTS(YEAR(CAST(r.submittedAtUtc AS DATETIME2)), MONTH(CAST(r.submittedAtUtc AS DATETIME2)), 1) AS SubmissionMonthStart,

    -- Diagnostic Categorization (Array unwrapping)
    r.diagnosisCodes,
    r.procedureCodes
FROM
    OPENROWSET(
        PROVIDER = 'CosmosDB',
        CONNECTION = 'AccountEndpoint=https://cosmos-healthcare-platform-dev.documents.azure.com:443/;Database=HealthcarePlatform',
        OBJECT = 'claims',
        SERVER_CREDENTIAL = 'CosmosDbCredential'
    )
    WITH (
        tenantId VARCHAR(50),
        claimId VARCHAR(50),
        memberId VARCHAR(50),
        providerId VARCHAR(50),
        claimAmount FLOAT,
        currency VARCHAR(10),
        claimStatus VARCHAR(30),
        serviceDate VARCHAR(10),
        submittedAtUtc VARCHAR(35),
        diagnosisCodes VARCHAR(MAX) AS JSON,
        procedureCodes VARCHAR(MAX) AS JSON
    ) AS r;
GO
