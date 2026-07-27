variable "name" {
  type = string
}

variable "github_oidc_provider_arn" {
  type = string
}

variable "github_oidc_repository_subject" {
  description = "Repository portion of GitHub's OIDC sub claim, including immutable IDs when GitHub emits them."
  type        = string
}

variable "github_environment" {
  type = string
}

variable "frontend_bucket_arn" {
  type = string
}

variable "backups_bucket_arn" {
  type = string
}

variable "cloudfront_distribution_arn" {
  type = string
}

variable "parameter_path_arn" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
