-- ==============================================================================
-- STEP 3: EXECUTIVE & TENANT ANALYTICAL REPORTING QUERIES
-- Target: Power BI Data Model, Synapse Notebooks, and Scheduled ETL
-- ==============================================================================

USE HealthcareAnalyticsDb;
GO

-- ------------------------------------------------------------------------------
-- REPORT 1: Total Claims Received & Volume Breakdown per Tenant
-- ------------------------------------------------------------------------------
SELECT
    TenantId,
    COUNT(1) AS TotalClaimsReceived,
    SUM(ClaimAmount) AS TotalBilledAmountUSD,
    ROUND(AVG(ClaimAmount), 2) AS AverageClaimAmountUSD,
    MIN(ClaimAmount) AS MinClaimAmountUSD,
    MAX(ClaimAmount) AS MaxClaimAmountUSD
FROM
    analytics.ClaimsReportingView
GROUP BY
    TenantId
ORDER BY
    TotalClaimsReceived DESC;

-- ------------------------------------------------------------------------------
-- REPORT 2: Claims Processing Status Distribution (Processed, Pending, Rejected)
-- ------------------------------------------------------------------------------
SELECT
    TenantId,
    ClaimStatus,
    COUNT(1) AS StatusCount,
    ROUND(CAST(COUNT(1) AS FLOAT) / SUM(COUNT(1)) OVER(PARTITION BY TenantId) * 100, 2) AS [StatusPercentage%],
    SUM(ClaimAmount) AS TotalAmountUSD
FROM
    analytics.ClaimsReportingView
GROUP BY
    TenantId,
    ClaimStatus
ORDER BY
    TenantId,
    StatusCount DESC;

-- ------------------------------------------------------------------------------
-- REPORT 3: Claims Ingestion Volume Trends (Monthly Aggregation)
-- ------------------------------------------------------------------------------
SELECT
    SubmissionYear,
    SubmissionMonth,
    SubmissionMonthStart,
    TenantId,
    COUNT(1) AS MonthlyClaimsCount,
    SUM(ClaimAmount) AS MonthlyBilledVolumeUSD,
    ROUND(AVG(ClaimAmount), 2) AS MonthlyAvgClaimAmountUSD
FROM
    analytics.ClaimsReportingView
GROUP BY
    SubmissionYear,
    SubmissionMonth,
    SubmissionMonthStart,
    TenantId
ORDER BY
    SubmissionYear DESC,
    SubmissionMonth DESC,
    TenantId;

-- ------------------------------------------------------------------------------
-- REPORT 4: Provider Volume & Network Analysis
-- ------------------------------------------------------------------------------
SELECT TOP 25
    TenantId,
    ProviderId,
    COUNT(1) AS ClaimsSubmittedCount,
    SUM(ClaimAmount) AS TotalBilledUSD,
    ROUND(AVG(ClaimAmount), 2) AS AvgClaimUSD
FROM
    analytics.ClaimsReportingView
GROUP BY
    TenantId,
    ProviderId
ORDER BY
    ClaimsSubmittedCount DESC;

-- ------------------------------------------------------------------------------
-- REPORT 5: Tenant-Wise Executive Summary Matrix (KPIs)
-- ------------------------------------------------------------------------------
SELECT
    TenantId,
    COUNT(1) AS TotalClaims,
    COUNT(DISTINCT ProviderId) AS ActiveProvidersCount,
    COUNT(DISTINCT AnonymizedMemberKey) AS ActiveMembersCount,
    SUM(CASE WHEN ClaimStatus = 'Submitted' THEN 1 ELSE 0 END) AS PendingClaimsCount,
    SUM(CASE WHEN ClaimStatus = 'Validated' THEN 1 ELSE 0 END) AS ValidatedClaimsCount,
    SUM(CASE WHEN ClaimStatus = 'Rejected' THEN 1 ELSE 0 END) AS RejectedClaimsCount,
    ROUND(SUM(ClaimAmount), 2) AS TotalFinancialLiabilityUSD,
    ROUND(AVG(ClaimAmount), 2) AS MeanClaimAmountUSD
FROM
    analytics.ClaimsReportingView
GROUP BY
    TenantId
ORDER BY
    TotalClaims DESC;
