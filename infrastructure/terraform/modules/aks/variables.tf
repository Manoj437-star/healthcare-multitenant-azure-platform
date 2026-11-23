variable "cluster_name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "dns_prefix" {
  type = string
}

variable "kubernetes_version" {
  type        = string
  default     = "1.36"
  description = "Kubernetes version for AKS cluster (defaulting to latest supported GA 1.36 in East US)"
}

variable "system_node_count" {
  type    = number
  default = 2
}

variable "min_node_count" {
  type    = number
  default = 2
}

variable "max_node_count" {
  type    = number
  default = 2
}

variable "node_vm_size" {
  type    = string
  default = "Standard_D2s_v4"
}

variable "subnet_id" {
  type    = string
  default = null
}

variable "acr_id" {
  type    = string
  default = ""
}

variable "enable_acr_pull" {
  type        = bool
  default     = true
  description = "Enable AcrPull role assignment for AKS kubelet identity on ACR"
}

variable "tags" {
  type    = map(string)
  default = {}
}
