variable "account_name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "database_name" {
  type    = string
  default = "HealthcarePlatform"
}

variable "max_throughput" {
  type    = number
  default = 4000
}

variable "analytical_storage_ttl" {
  type        = number
  default     = -1
  description = "Analytical store retention in seconds. -1 indicates infinite retention without affecting transactional RUs."
}

variable "tags" {
  type    = map(string)
  default = {}
}
