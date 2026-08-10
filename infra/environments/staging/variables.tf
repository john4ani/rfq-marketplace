variable "aws_region" {
  type    = string
  default = "eu-central-1"
}

variable "availability_zone" {
  type    = string
  default = "eu-central-1a"
}

variable "admin_cidr" {
  type = string
}

variable "github_oidc_repository_subject" {
  type    = string
  default = "john4ani@31954109/rfq-marketplace@1309971212"
}

variable "lightsail_bundle_id" {
  type    = string
  default = "small_3_0"
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

variable "frontend_domain" {
  type     = string
  default  = null
  nullable = true
}

variable "frontend_aliases" {
  type    = list(string)
  default = []
}

variable "frontend_certificate_arn" {
  type     = string
  default  = null
  nullable = true
}

variable "acme_email" {
  type = string
}

variable "smtp_from_address" {
  type = string
}

variable "create_ses_identity" {
  type    = bool
  default = true
}
