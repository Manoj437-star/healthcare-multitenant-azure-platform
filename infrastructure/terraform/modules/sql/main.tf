resource "azurerm_mssql_server" "sql_server" {
  name                         = var.server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.admin_username
  administrator_login_password = var.admin_password
  minimum_tls_version          = "1.2"
  tags                         = var.tags
}

resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.sql_server.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Dedicated Database for Tier 1 Enterprise Tenant (Tenant A)
resource "azurerm_mssql_database" "db_tenant_a" {
  name        = "tenant-a-db"
  server_id   = azurerm_mssql_server.sql_server.id
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  sku_name    = var.sku_name
  max_size_gb = 50
  tags        = merge(var.tags, { tenant_id = "tenant-a", tier = "dedicated" })
}

# Pooled Database for Tier 2 Standard Tenants (Tenant B and others)
resource "azurerm_mssql_database" "db_shared_payers" {
  name        = "shared-payers-db"
  server_id   = azurerm_mssql_server.sql_server.id
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  sku_name    = var.sku_name
  max_size_gb = 50
  tags        = merge(var.tags, { tenant_id = "shared-platform", tier = "pooled" })
}

output "server_id" {
  value = azurerm_mssql_server.sql_server.id
}

output "server_fqdn" {
  value = azurerm_mssql_server.sql_server.fully_qualified_domain_name
}

output "tenant_a_db_name" {
  value = azurerm_mssql_database.db_tenant_a.name
}

output "shared_payers_db_name" {
  value = azurerm_mssql_database.db_shared_payers.name
}
