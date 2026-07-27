output "distribution_id" {
  value = aws_cloudfront_distribution.this.id
}

output "distribution_arn" {
  value = aws_cloudfront_distribution.this.arn
}

output "distribution_domain_name" {
  value = aws_cloudfront_distribution.this.domain_name
}

output "frontend_origin" {
  value = "https://${local.use_custom_domain ? var.frontend_domain : aws_cloudfront_distribution.this.domain_name}"
}
