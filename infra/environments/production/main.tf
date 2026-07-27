module "environment" {
  source = "../../modules/environment"
  providers = {
    aws           = aws
    aws.us_east_1 = aws.us_east_1
  }

  environment         = "production"
  aws_region          = var.aws_region
  availability_zone   = var.availability_zone
  admin_cidr          = var.admin_cidr
  github_repository   = var.github_repository
  lightsail_bundle_id = var.lightsail_bundle_id
  root_domain         = var.root_domain
  hosted_zone_id      = var.hosted_zone_id
  acme_email          = var.acme_email
  smtp_from_address   = var.smtp_from_address
}
