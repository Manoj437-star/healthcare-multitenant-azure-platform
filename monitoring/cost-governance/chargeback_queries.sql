-- ==============================================================================
-- HEALTHCARE MULTI-TENANT PLATFORM: FINOPS & CHARGEBACK ANALYSIS QUERIES
-- Target: Azure Synapse Analytics / Azure SQL Serverless / Fabric Lakehouse
-- Source: Azure Cost Management Scheduled Billing Export (Amortized Cost Dataset)
-- ==============================================================================

-- 1. Monthly Direct Spend per Tenant
SELECT
    COALESCE(JSON_VALUE(Tags, '$.tenant_id'), 'shared-platform') AS TenantId,
    JSON_VALUE(Tags, '$.environment') AS Environment,
    ServiceName,
    MeterCategory,
    FORMAT(UsageDateTime, 'yyyy-MM') AS BillingMonth,
    ROUND(SUM(CostInBillingCurrency), 2) AS DirectCostUSD,
    BillingCurrency
FROM
    dbo.AzureCostExport
WHERE
    UsageDateTime >= DATEADD(MONTH, -3, GETUTCDATE())
GROUP BY
    COALESCE(JSON_VALUE(Tags, '$.tenant_id'), 'shared-platform'),
    JSON_VALUE(Tags, '$.environment'),
    ServiceName,
    MeterCategory,
    FORMAT(UsageDateTime, 'yyyy-MM'),
    BillingCurrency
ORDER BY
    BillingMonth DESC,
    DirectCostUSD DESC;

-- 2. Hybrid Chargeback Model (Direct Cost + Proportional Shared Infrastructure Allocation)
-- Formula: Tenant_Chargeback = Tenant_Direct_Cost + (Shared_Cost * (Tenant_Claim_Volume / Total_Claim_Volume))

WITH TenantClaimVolumes AS (
    SELECT
        tenantId,
        COUNT(1) AS MonthlyClaimsCount,
        CAST(COUNT(1) AS FLOAT) / SUM(COUNT(1)) OVER() AS VolumeRatio
    FROM
        dbo.ClaimsSummary
    WHERE
        AdjudicatedDate >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1)
    GROUP BY
        tenantId
),
DirectCosts AS (
    SELECT
        JSON_VALUE(Tags, '$.tenant_id') AS TenantId,
        SUM(CostInBillingCurrency) AS TotalDirectCost
    FROM
        dbo.AzureCostExport
    WHERE
        JSON_VALUE(Tags, '$.tenant_id') IS NOT NULL
        AND UsageDateTime >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1)
    GROUP BY
        JSON_VALUE(Tags, '$.tenant_id')
),
SharedInfrastructureCost AS (
    SELECT
        SUM(CostInBillingCurrency) AS TotalSharedCost
    FROM
        dbo.AzureCostExport
    WHERE
        (JSON_VALUE(Tags, '$.tenant_id') IS NULL OR JSON_VALUE(Tags, '$.tenant_id') = 'shared-platform')
        AND UsageDateTime >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1)
)
SELECT
    v.tenantId AS TenantId,
    v.MonthlyClaimsCount,
    ROUND(v.VolumeRatio * 100, 2) AS [VolumePercentage%],
    ROUND(COALESCE(d.TotalDirectCost, 0), 2) AS DirectInfrastructureCostUSD,
    ROUND(s.TotalSharedCost * v.VolumeRatio, 2) AS AllocatedSharedCostUSD,
    ROUND(COALESCE(d.TotalDirectCost, 0) + (s.TotalSharedCost * v.VolumeRatio), 2) AS TotalTenantInvoiceUSD,
    ROUND((COALESCE(d.TotalDirectCost, 0) + (s.TotalSharedCost * v.VolumeRatio)) / NULLIF(v.MonthlyClaimsCount, 0), 4) AS CostPerClaimUSD
FROM
    TenantClaimVolumes v
LEFT JOIN
    DirectCosts d ON v.tenantId = d.TenantId
CROSS JOIN
    SharedInfrastructureCost s
ORDER BY
    TotalTenantInvoiceUSD DESC;
