-- ==============================================================================
-- STEP 4: TENANT ROW-LEVEL SECURITY (RLS) FOR SYNAPSE SERVERLESS SQL POOL
-- ==============================================================================
-- ARCHITECTURAL NOTE ON SYNAPSE SERVERLESS SQL POOL CAPABILITIES:
-- Azure Synapse Serverless SQL Pool (built-in on-demand) does NOT support
-- user-defined functions (CREATE FUNCTION) or CREATE SECURITY POLICY (native RLS).
-- These features are exclusively supported in Azure Synapse Dedicated SQL Pools and Azure SQL.
--
-- SUPPORTED SECURE PATTERN IN SERVERLESS SQL POOL:
-- 1. Secure multi-tenant views with inline authorization checks:
--    - Evaluates IS_ROLEMEMBER() for database-level role isolation.
--    - Evaluates SESSION_CONTEXT(N'TenantId') for connection/app-level tenant filtering.
-- 2. Dedicated tenant-scoped views (e.g. TenantAClaimsView) with granular
--    GRANT / DENY object permissions for external reporting identities.
-- ==============================================================================

USE HealthcareAnalyticsDb;
GO

-- ------------------------------------------------------------------------------
-- 1. Create Tenant-Specific Database Roles
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'PlatformAdmin' AND type = 'R')
    CREATE ROLE PlatformAdmin;
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'TenantARole' AND type = 'R')
    CREATE ROLE TenantARole;
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'TenantBRole' AND type = 'R')
    CREATE ROLE TenantBRole;
GO

-- ------------------------------------------------------------------------------
-- 2. Multi-Tenant Secure View (Using Inline Security Logic without UDFs)
-- ------------------------------------------------------------------------------
CREATE OR ALTER VIEW analytics.SecureClaimsReportingView
AS
SELECT *
FROM analytics.ClaimsReportingView
WHERE
    -- Platform Admins have full access
    IS_ROLEMEMBER('PlatformAdmin') = 1
    OR IS_ROLEMEMBER('db_owner') = 1

    -- Role-based tenant isolation
    OR (IS_ROLEMEMBER('TenantARole') = 1 AND TenantId = 'tenant-a')
    OR (IS_ROLEMEMBER('TenantBRole') = 1 AND TenantId = 'tenant-b')

    -- Session context isolation for Power BI Embedded / Web API service connections
    OR (CAST(SESSION_CONTEXT(N'TenantId') AS VARCHAR(50)) = TenantId);
GO

-- ------------------------------------------------------------------------------
-- 3. Dedicated Tenant-Specific Views for Payer Reporting
-- ------------------------------------------------------------------------------
CREATE OR ALTER VIEW analytics.TenantAClaimsView
AS
SELECT *
FROM analytics.ClaimsReportingView
WHERE TenantId = 'tenant-a';
GO

CREATE OR ALTER VIEW analytics.TenantBClaimsView
AS
SELECT *
FROM analytics.ClaimsReportingView
WHERE TenantId = 'tenant-b';
GO

-- ------------------------------------------------------------------------------
-- 4. Granular Permission Grants
-- ------------------------------------------------------------------------------
-- Grant Tenant A access strictly to their dedicated view and secure view
GRANT SELECT ON analytics.TenantAClaimsView TO TenantARole;
GRANT SELECT ON analytics.SecureClaimsReportingView TO TenantARole;

-- Grant Tenant B access strictly to their dedicated view and secure view
GRANT SELECT ON analytics.TenantBClaimsView TO TenantBRole;
GRANT SELECT ON analytics.SecureClaimsReportingView TO TenantBRole;

-- Grant Platform Admins full access
GRANT SELECT ON analytics.ClaimsReportingView TO PlatformAdmin;
GRANT SELECT ON analytics.SecureClaimsReportingView TO PlatformAdmin;
GO
