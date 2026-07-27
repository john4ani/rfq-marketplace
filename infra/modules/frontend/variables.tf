variable "name" {
  type = string
}

variable "frontend_bucket_id" {
  type = string
}

variable "frontend_bucket_arn" {
  type = string
}

variable "frontend_bucket_domain_name" {
  type = string
}

variable "api_origin_domain" {
  type = string
}

variable "frontend_domain" {
  type     = string
  default  = null
  nullable = true
}

variable "hosted_zone_id" {
  type     = string
  default  = null
  nullable = true
}

variable "tags" {
  type    = map(string)
  default = {}
}
