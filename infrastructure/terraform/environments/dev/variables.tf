variable "environment" {
  type        = string
  default     = "dev"
  description = "Target deployment environment (dev, staging, prod)"
}

variable "location" {
  type        = string
  default     = "eastus"
  description = "Azure region for deployment"
}

variable "data_tier_location" {
  type        = string
  default     = "centralindia"
  description = "Azure region for data-tier persistence services (Azure SQL and Cosmos DB) to circumvent East US quota restrictions"
}

variable "resource_group_name" {
  type        = string
  default     = "rg-healthcare-platform-dev"
  description = "Resource Group Name"
}

variable "sql_admin_password" {
  type        = string
  description = "Administrator password for Azure SQL server"
  sensitive   = true
}

variable "enable_synapse" {
  type        = bool
  default     = false
  description = "Enable Azure Synapse Analytics Workspace (set to true when deploying analytical read model)"
}

variable "tags" {
  type = map(string)
  default = {
    Project     = "Healthcare-MultiTenant-Platform"
    Environment = "dev"
    ManagedBy   = "Terraform"
    Compliance  = "HIPAA-Aligned"
  }
}
