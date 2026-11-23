output "resource_group_name" {
  value = azurerm_resource_group.rg.name
}

output "acr_login_server" {
  value = module.acr.login_server
}

output "aks_cluster_name" {
  value = module.aks.cluster_name
}

output "aks_oidc_issuer_url" {
  value = module.aks.oidc_issuer_url
}

output "key_vault_uri" {
  value = module.keyvault.vault_uri
}

output "servicebus_namespace" {
  value = module.servicebus.namespace_name
}

output "sql_server_fqdn" {
  value = module.sql.server_fqdn
}

output "cosmosdb_endpoint" {
  value = module.cosmosdb.endpoint
}

output "apim_gateway_url" {
  value = module.apim.gateway_url
}

output "synapse_serverless_sql_endpoint" {
  value = var.enable_synapse ? module.synapse[0].serverless_sql_endpoint : "Synapse Disabled"
}
