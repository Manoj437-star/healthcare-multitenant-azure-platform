resource "azurerm_servicebus_namespace" "sb" {
  name                = var.namespace_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku
  capacity            = var.sku == "Premium" ? 1 : 0
  tags                = var.tags
}

resource "azurerm_servicebus_topic" "member_events" {
  name         = "member-events"
  namespace_id = azurerm_servicebus_namespace.sb.id
}

resource "azurerm_servicebus_subscription" "claims_ingestion_member_sub" {
  name                                 = "sub-claims-ingestion-member-enrolled"
  topic_id                             = azurerm_servicebus_topic.member_events.id
  max_delivery_count                   = 5
  dead_lettering_on_message_expiration = true
}

resource "azurerm_servicebus_subscription_rule" "member_enrolled_rule" {
  name            = "MemberEnrolledFilter"
  subscription_id = azurerm_servicebus_subscription.claims_ingestion_member_sub.id
  filter_type     = "SqlFilter"
  sql_filter      = "EventType = 'MemberEnrolled'"
}

resource "azurerm_servicebus_topic" "claim_events" {
  name         = "claim-events"
  namespace_id = azurerm_servicebus_namespace.sb.id
}

resource "azurerm_servicebus_subscription" "adjudication_claim_sub" {
  name               = "sub-adjudication-claim-received"
  topic_id           = azurerm_servicebus_topic.claim_events.id
  max_delivery_count = 5
}

output "namespace_id" {
  value = azurerm_servicebus_namespace.sb.id
}

output "namespace_name" {
  value = azurerm_servicebus_namespace.sb.name
}

output "primary_connection_string" {
  value     = azurerm_servicebus_namespace.sb.default_primary_connection_string
  sensitive = true
}
