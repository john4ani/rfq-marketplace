output "frontend_bucket_id" {
  value = aws_s3_bucket.this["frontend"].id
}

output "frontend_bucket_arn" {
  value = aws_s3_bucket.this["frontend"].arn
}

output "frontend_bucket_domain_name" {
  value = aws_s3_bucket.this["frontend"].bucket_regional_domain_name
}

output "attachments_bucket_id" {
  value = aws_s3_bucket.this["attachments"].id
}

output "attachments_bucket_arn" {
  value = aws_s3_bucket.this["attachments"].arn
}

output "backups_bucket_id" {
  value = aws_s3_bucket.this["backups"].id
}

output "backups_bucket_arn" {
  value = aws_s3_bucket.this["backups"].arn
}
