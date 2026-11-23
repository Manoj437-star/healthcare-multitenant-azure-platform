resource "azurerm_storage_account" "datalake" {
  name                     = var.storage_account_name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"
  is_hns_enabled           = true # Hierarchical Namespace for ADLS Gen2

  tags = var.tags
}

resource "azurerm_storage_data_lake_gen2_filesystem" "synapse_fs" {
  name               = "synapse-workspace-fs"
  storage_account_id = azurerm_storage_account.datalake.id
}

resource "azurerm_synapse_workspace" "synapse" {
  name                                 = var.synapse_name
  resource_group_name                  = var.resource_group_name
  location                             = var.location
  storage_data_lake_gen2_filesystem_id = azurerm_storage_data_lake_gen2_filesystem.synapse_fs.id
  sql_administrator_login              = "sqladmin"
  sql_administrator_login_password     = var.admin_password

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

resource "azurerm_synapse_firewall_rule" "allow_azure_services" {
  name                 = "AllowAllWindowsAzureIps"
  synapse_workspace_id = azurerm_synapse_workspace.synapse.id
  start_ip_address     = "0.0.0.0"
  end_ip_address       = "0.0.0.0"
}

output "workspace_id" {
  value = azurerm_synapse_workspace.synapse.id
}

output "serverless_sql_endpoint" {
  value = azurerm_synapse_workspace.synapse.connectivity_endpoints["sqlOnDemand"]
}

output "principal_id" {
  value = azurerm_synapse_workspace.synapse.identity[0].principal_id
}
