variable "acr_name" {
  type        = string
  description = "Name of the Azure Container Registry"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the resource group"
}

variable "location" {
  type        = string
  description = "Azure region"
}

variable "sku" {
  type        = string
  default     = "Standard"
  description = "ACR SKU (Basic, Standard, Premium)"
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Resource tags"
}
