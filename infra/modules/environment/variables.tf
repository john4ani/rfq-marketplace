variable "project_name" {
  type    = string
  default = "rfq-marketplace"
}

variable "environment" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "availability_zone" {
  type = string
}

variable "lightsail_bundle_id" {
  type    = string
  default = "small_3_0"
}

variable "admin_cidr" {
  type = string
}

variable "github_repository" {
  type = string
}

variable "root_domain" {
  type     = string
  default  = null
  nullable = true
}

variable "hosted_zone_id" {
  type     = string
  default  = null
  nullable = true
}

variable "frontend_subdomain" {
  type    = string
  default = "app"
}

variable "api_subdomain" {
  type    = string
  default = "api"
}

variable "acme_email" {
  type = string
}

variable "smtp_from_address" {
  type = string
}

variable "smtp_from_name" {
  type    = string
  default = "RFQ Marketplace"
}

variable "create_ses_identity" {
  type    = bool
  default = true
}

variable "tags" {
  type    = map(string)
  default = {}
}
