output "deployment" {
  value = {
    api_origin              = module.environment.api_origin
    frontend_origin         = module.environment.frontend_origin
    cloudfront_distribution = module.environment.cloudfront_distribution_id
    frontend_bucket         = module.environment.frontend_bucket
    attachments_bucket      = module.environment.attachments_bucket
    backups_bucket          = module.environment.backups_bucket
    lightsail_instance_name = module.environment.lightsail_instance_name
    lightsail_static_ip     = module.environment.lightsail_static_ip
    github_deploy_role_arn  = module.environment.github_deploy_role_arn
    parameter_path          = module.environment.parameter_path
    alerts_topic_arn        = module.environment.alerts_topic_arn
    ses_smtp_user_name      = module.environment.ses_smtp_user_name
  }
}
