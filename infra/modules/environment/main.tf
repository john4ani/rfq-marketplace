data "aws_caller_identity" "current" {}
data "aws_partition" "current" {}

locals {
  name              = "${var.project_name}-${var.environment}"
  has_custom_domain = var.root_domain != null && var.hosted_zone_id != null
  frontend_domain   = local.has_custom_domain ? "${var.frontend_subdomain}.${var.root_domain}" : null
  bucket_prefix     = "${var.project_name}-${var.environment}-${data.aws_caller_identity.current.account_id}-${var.aws_region}"
  parameter_path    = "/${var.project_name}/${var.environment}"
  oidc_provider_arn = "arn:${data.aws_partition.current.partition}:iam::${data.aws_caller_identity.current.account_id}:oidc-provider/token.actions.githubusercontent.com"
  common_tags = merge(var.tags, {
    Application = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  })
}

module "host" {
  source = "../lightsail-host"

  name              = local.name
  availability_zone = var.availability_zone
  bundle_id         = var.lightsail_bundle_id
  admin_cidr        = var.admin_cidr
  tags              = local.common_tags
}

locals {
  api_origin_domain = local.has_custom_domain ? "${var.api_subdomain}.${var.root_domain}" : "${replace(module.host.static_ip, ".", "-")}.sslip.io"
}

resource "aws_route53_record" "api" {
  count = local.has_custom_domain ? 1 : 0

  zone_id = var.hosted_zone_id
  name    = local.api_origin_domain
  type    = "A"
  ttl     = 60
  records = [module.host.static_ip]
}

module "storage" {
  source = "../storage"

  bucket_prefix = local.bucket_prefix
  tags          = local.common_tags
}

module "frontend" {
  source = "../frontend"
  providers = {
    aws           = aws
    aws.us_east_1 = aws.us_east_1
  }

  name                        = local.name
  frontend_bucket_id          = module.storage.frontend_bucket_id
  frontend_bucket_arn         = module.storage.frontend_bucket_arn
  frontend_bucket_domain_name = module.storage.frontend_bucket_domain_name
  api_origin_domain           = local.api_origin_domain
  frontend_domain             = local.frontend_domain
  hosted_zone_id              = var.hosted_zone_id
  tags                        = local.common_tags
}

resource "aws_sesv2_email_identity" "sender" {
  count = var.create_ses_identity ? 1 : 0

  email_identity = var.smtp_from_address
  tags           = local.common_tags
}

resource "aws_iam_user" "ses_smtp" {
  name = "${local.name}-ses-smtp"
  tags = local.common_tags
}

data "aws_iam_policy_document" "ses_smtp" {
  statement {
    sid = "SendMarketplaceEmail"
    actions = [
      "ses:SendEmail",
      "ses:SendRawEmail"
    ]
    resources = ["*"]
  }
}

resource "aws_iam_user_policy" "ses_smtp" {
  name   = "${local.name}-ses-send"
  user   = aws_iam_user.ses_smtp.name
  policy = data.aws_iam_policy_document.ses_smtp.json
}

locals {
  runtime_parameters = {
    "acme-email"                 = var.acme_email
    "admin-cidr"                 = var.admin_cidr
    "api-origin-host"            = local.api_origin_domain
    "attachments-bucket"         = module.storage.attachments_bucket_id
    "backups-bucket"             = module.storage.backups_bucket_id
    "cloudfront-distribution-id" = module.frontend.distribution_id
    "frontend-bucket"            = module.storage.frontend_bucket_id
    "frontend-origin"            = module.frontend.frontend_origin
    "instance-name"              = module.host.instance_name
    "jwt-audience"               = "rfq-marketplace-web"
    "jwt-issuer"                 = "rfq-marketplace-api"
    "postgres-database"          = "marketplace"
    "postgres-user"              = "marketplace"
    "smtp-from-address"          = var.smtp_from_address
    "smtp-from-name"             = var.smtp_from_name
    "smtp-host"                  = "email-smtp.${var.aws_region}.amazonaws.com"
    "smtp-port"                  = "587"
  }
}

resource "aws_ssm_parameter" "runtime" {
  for_each = local.runtime_parameters

  name  = "${local.parameter_path}/${each.key}"
  type  = "String"
  value = each.value
  tags  = local.common_tags
}

module "github_deploy_role" {
  source = "../github-deploy-role"

  name                        = "${local.name}-github-deploy"
  github_oidc_provider_arn    = local.oidc_provider_arn
  github_repository           = var.github_repository
  github_environment          = var.environment
  frontend_bucket_arn         = module.storage.frontend_bucket_arn
  backups_bucket_arn          = module.storage.backups_bucket_arn
  cloudfront_distribution_arn = module.frontend.distribution_arn
  parameter_path_arn          = "arn:${data.aws_partition.current.partition}:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter${local.parameter_path}/"
  tags                        = local.common_tags
}

module "monitoring" {
  source = "../monitoring"

  name          = local.name
  instance_name = module.host.instance_name
  alert_email   = var.acme_email
  tags          = local.common_tags
}
