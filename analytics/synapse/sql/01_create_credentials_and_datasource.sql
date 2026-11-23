-- ==============================================================================
-- STEP 1: INITIALIZE DATABASE, CREDENTIALS AND COSMOS DB DATA SOURCE
-- Target: Azure Synapse Serverless SQL Pool (or Fabric SQL Endpoint)
-- ==============================================================================

-- 1. Create Dedicated Analytics Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HealthcareAnalyticsDb')
BEGIN
    CREATE DATABASE HealthcareAnalyticsDb;
END
GO

USE HealthcareAnalyticsDb;
GO

-- 2. Create Schema for Analytics and Security
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'analytics')
BEGIN
    EXEC('CREATE SCHEMA analytics');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'security')
BEGIN
    EXEC('CREATE SCHEMA security');
END
GO

-- 3. Create Master Key for Encryption
IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name LIKE '%DatabaseMasterKey%')
BEGIN
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'SynapseMasterKeyStrongP@ss2026!';
END
GO

-- 4. Create Database Scoped Credential for Cosmos DB Analytical Store Access
-- Note: Replace with Azure Managed Identity or Key Vault linked secret in production
IF NOT EXISTS (SELECT * FROM sys.database_scoped_credentials WHERE name = 'CosmosDbCredential')
BEGIN
    CREATE DATABASE SCOPED CREDENTIAL CosmosDbCredential
    WITH IDENTITY = 'SHARED ACCESS SIGNATURE',
         SECRET = 'AccountEndpoint=https://cosmos-healthcare-platform-dev.documents.azure.com:443/;Database=HealthcarePlatform;';
END
GO
