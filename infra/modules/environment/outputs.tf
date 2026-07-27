output "api_origin" {
  value = "https://${local.api_origin_domain}"
}

output "frontend_origin" {
  value = module.frontend.frontend_origin
}

output "cloudfront_distribution_id" {
  value = module.frontend.distribution_id
}

output "frontend_bucket" {
  value = module.storage.frontend_bucket_id
}

output "attachments_bucket" {
  value = module.storage.attachments_bucket_id
}

output "backups_bucket" {
  value = module.storage.backups_bucket_id
}

output "lightsail_instance_name" {
  value = module.host.instance_name
}

output "lightsail_static_ip" {
  value = module.host.static_ip
}

output "github_deploy_role_arn" {
  value = module.github_deploy_role.role_arn
}

output "parameter_path" {
  value = local.parameter_path
}

output "alerts_topic_arn" {
  value = module.monitoring.alerts_topic_arn
}
