variable "aws_region" {
  description = "Region that stores the Terraform state bucket."
  type        = string
  default     = "eu-central-1"
}

variable "state_bucket_name" {
  description = "Globally unique S3 bucket name for Terraform state."
  type        = string
}

variable "tags" {
  description = "Tags applied to bootstrap resources."
  type        = map(string)
  default = {
    Application = "rfq-marketplace"
    ManagedBy   = "terraform"
  }
}
