output "state_bucket_name" {
  description = "Bucket used by the environment roots."
  value       = aws_s3_bucket.terraform_state.id
}

output "github_oidc_provider_arn" {
  description = "GitHub Actions OIDC provider shared by all environments."
  value       = aws_iam_openid_connect_provider.github_actions.arn
}

output "monthly_cost_budget_name" {
  description = "Account-wide monthly AWS cost budget."
  value       = aws_budgets_budget.account_monthly_cost.name
}
