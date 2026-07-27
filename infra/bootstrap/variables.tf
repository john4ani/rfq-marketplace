variable "aws_region" {
  description = "Region that stores the Terraform state bucket."
  type        = string
  default     = "eu-central-1"
}

variable "state_bucket_name" {
  description = "Globally unique S3 bucket name for Terraform state."
  type        = string
}

variable "monthly_budget_limit_usd" {
  description = "Account-wide monthly AWS cost budget in USD."
  type        = number
  default     = 20

  validation {
    condition     = var.monthly_budget_limit_usd > 0
    error_message = "The monthly AWS budget must be greater than zero."
  }
}

variable "budget_alert_email" {
  description = "Email address that receives account cost budget alerts."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.budget_alert_email))
    error_message = "The budget alert email must be a valid email address."
  }
}

variable "tags" {
  description = "Tags applied to bootstrap resources."
  type        = map(string)
  default = {
    Application = "rfq-marketplace"
    ManagedBy   = "terraform"
  }
}
