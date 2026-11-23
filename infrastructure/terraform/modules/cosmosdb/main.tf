resource "azurerm_cosmosdb_account" "cosmos" {
  name                = var.account_name
  location            = var.location
  resource_group_name = var.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  consistency_policy {
    consistency_level       = "Session"
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  geo_location {
    location          = var.location
    failover_priority = 0
  }

  # Enable Analytical Storage for Azure Synapse Link / Fabric HTAP
  analytical_storage {
    schema_type = "WellDefined" # Optimized for structured healthcare datasets
  }

  timeouts {
    create = "60m"
    update = "60m"
    read   = "10m"
    delete = "30m"
  }

  tags = var.tags
}

resource "azurerm_cosmosdb_sql_database" "db" {
  name                = var.database_name
  resource_group_name = var.resource_group_name
  account_name        = azurerm_cosmosdb_account.cosmos.name
}

resource "azurerm_cosmosdb_sql_container" "claims" {
  name                  = "claims"
  resource_group_name   = var.resource_group_name
  account_name          = azurerm_cosmosdb_account.cosmos.name
  database_name         = azurerm_cosmosdb_sql_database.db.name
  partition_key_paths   = ["/tenantId"]
  partition_key_version = 2

  autoscale_settings {
    max_throughput = var.max_throughput
  }
}

resource "azurerm_cosmosdb_sql_container" "members" {
  name                  = "enrolled-members"
  resource_group_name   = var.resource_group_name
  account_name          = azurerm_cosmosdb_account.cosmos.name
  database_name         = azurerm_cosmosdb_sql_database.db.name
  partition_key_paths   = ["/tenantId"]
  partition_key_version = 2

  autoscale_settings {
    max_throughput = 1000
  }
}

resource "azurerm_cosmosdb_sql_container" "idempotency" {
  name                  = "idempotency-records"
  resource_group_name   = var.resource_group_name
  account_name          = azurerm_cosmosdb_account.cosmos.name
  database_name         = azurerm_cosmosdb_sql_database.db.name
  partition_key_paths   = ["/tenantId"]
  partition_key_version = 2

  default_ttl = 604800 # 7 days retention
}

output "account_id" {
  value = azurerm_cosmosdb_account.cosmos.id
}

output "endpoint" {
  value = azurerm_cosmosdb_account.cosmos.endpoint
}

output "primary_key" {
  value     = azurerm_cosmosdb_account.cosmos.primary_key
  sensitive = true
}
