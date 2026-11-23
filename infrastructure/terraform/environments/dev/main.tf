resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

module "monitoring" {
  source              = "../../modules/monitoring"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  workspace_name      = "law-healthcare-platform-${var.environment}"
  appinsights_name    = "appi-healthcare-platform-${var.environment}"
  tags                = var.tags
}

module "acr" {
  source              = "../../modules/acr"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  acr_name            = "acrhealthcareplatform${var.environment}"
  sku                 = "Standard"
  tags                = var.tags
}

module "keyvault" {
  source              = "../../modules/keyvault"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  vault_name          = "kv-hlth-${var.environment}-01"
  tags                = var.tags
}

module "servicebus" {
  source              = "../../modules/servicebus"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  namespace_name      = "asb-healthcare-platform-${var.environment}"
  sku                 = "Standard"
  tags                = var.tags
}

module "sql" {
  source              = "../../modules/sql"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.data_tier_location
  server_name         = "sql-healthcare-platform-${var.environment}"
  admin_username      = "sqladmin"
  admin_password      = var.sql_admin_password
  sku_name            = "S0"
  tags                = var.tags
}

module "cosmosdb" {
  source              = "../../modules/cosmosdb"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.data_tier_location
  account_name        = "cosmos-healthcare-platform-${var.environment}"
  database_name       = "HealthcarePlatform"
  tags                = var.tags
}

module "aks" {
  source              = "../../modules/aks"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  cluster_name        = "aks-healthcare-platform-${var.environment}"
  dns_prefix          = "aks-healthcare-${var.environment}"
  system_node_count   = 2
  node_vm_size        = "Standard_D2s_v4"
  acr_id              = module.acr.acr_id
  tags                = var.tags
}

module "apim" {
  source              = "../../modules/apim"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  apim_name           = "apim-healthcare-${var.environment}-01"
  sku_name            = "Developer_1"
  tags                = var.tags
}

module "synapse" {
  count                = var.enable_synapse ? 1 : 0
  source               = "../../modules/synapse"
  resource_group_name  = azurerm_resource_group.rg.name
  location             = azurerm_resource_group.rg.location
  synapse_name         = "syn-healthcare-${var.environment}-01"
  storage_account_name = "stsynhlth${var.environment}01"
  admin_password       = var.sql_admin_password
  tags                 = var.tags
}
