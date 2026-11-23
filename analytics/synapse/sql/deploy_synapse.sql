-- ==============================================================================
-- MASTER DEPLOYMENT SCRIPT: SYNAPSE SERVERLESS SQL READ MODEL
-- Execution Mode: SQLCMD (:r commands)
-- Target: <workspace>-ondemand.sql.azuresynapse.net
-- Flag: -b (Stop immediately on failure)
-- ==============================================================================

:on error exit

PRINT 'Starting Step 1: Initialize Database, Credentials, and Data Source...'
:r 01_create_credentials_and_datasource.sql
PRINT 'Step 1 completed successfully.'

PRINT 'Starting Step 2: Create Claims Reporting Views over Cosmos DB Analytical Store...'
:r 02_create_views.sql
PRINT 'Step 2 completed successfully.'

PRINT 'Starting Step 3: Validate Analytical Queries...'
:r 03_analytical_queries.sql
PRINT 'Step 3 completed successfully.'

PRINT 'Starting Step 4: Configure Tenant Row-Level Security and Permissions...'
:r 04_tenant_security_rls.sql
PRINT 'Step 4 completed successfully.'

PRINT 'All Synapse Analytics Read Model deployment scripts executed successfully.'
