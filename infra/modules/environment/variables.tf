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

variable "github_oidc_repository_subject" {
  description = "Repository portion of GitHub's OIDC sub claim, including immutable IDs when GitHub emits them."
  type        = string
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
  description = "Canonical frontend domain. Set this directly for DNS hosted outside Route 53."
  type        = string
  default     = null
  nullable    = true
}

variable "frontend_aliases" {
  description = "Additional frontend domains, such as www, served by the same CloudFront distribution."
  type        = list(string)
  default     = []
}

variable "frontend_certificate_arn" {
  description = "Existing issued ACM certificate ARN in us-east-1 when DNS validation is external to Route 53."
  type        = string
  default     = null
  nullable    = true
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
  default = "CompaSpot"
}

variable "smtp_reply_to_address" {
  type = string
}

variable "create_ses_identity" {
  type    = bool
  default = true
}

variable "tags" {
  type    = map(string)
  default = {}
}
