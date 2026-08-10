module "environment" {
  source = "../../modules/environment"
  providers = {
    aws           = aws
    aws.us_east_1 = aws.us_east_1
  }

  environment                    = "staging"
  aws_region                     = var.aws_region
  availability_zone              = var.availability_zone
  admin_cidr                     = var.admin_cidr
  github_oidc_repository_subject = var.github_oidc_repository_subject
  lightsail_bundle_id            = var.lightsail_bundle_id
  root_domain                    = var.root_domain
  hosted_zone_id                 = var.hosted_zone_id
  frontend_domain                = var.frontend_domain
  frontend_aliases               = var.frontend_aliases
  frontend_certificate_arn       = var.frontend_certificate_arn
  acme_email                     = var.acme_email
  smtp_from_address              = var.smtp_from_address
  create_ses_identity            = var.create_ses_identity
}
