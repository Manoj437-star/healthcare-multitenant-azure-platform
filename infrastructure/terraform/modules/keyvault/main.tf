data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "kv" {
  name                       = var.vault_name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = var.sku_name
  soft_delete_retention_days = 90
  purge_protection_enabled   = var.purge_protection_enabled
  enable_rbac_authorization  = true

  network_acls {
    default_action = "Allow"
    bypass         = "AzureServices"
  }

  tags = var.tags
}

output "vault_id" {
  value = azurerm_key_vault.kv.id
}

output "vault_uri" {
  value = azurerm_key_vault.kv.vault_uri
}
