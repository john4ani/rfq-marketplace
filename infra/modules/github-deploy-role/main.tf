data "aws_iam_policy_document" "trust" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [var.github_oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_repository}:environment:${var.github_environment}"]
    }
  }
}

resource "aws_iam_role" "this" {
  name                 = var.name
  assume_role_policy   = data.aws_iam_policy_document.trust.json
  max_session_duration = 3600
  tags                 = var.tags
}

data "aws_iam_policy_document" "deploy" {
  statement {
    sid = "FrontendBucket"
    actions = [
      "s3:ListBucket",
      "s3:GetBucketLocation"
    ]
    resources = [var.frontend_bucket_arn]
  }

  statement {
    sid = "FrontendObjects"
    actions = [
      "s3:DeleteObject",
      "s3:GetObject",
      "s3:PutObject"
    ]
    resources = ["${var.frontend_bucket_arn}/*"]
  }

  statement {
    sid = "DatabaseBackups"
    actions = [
      "s3:ListBucket",
      "s3:GetBucketLocation"
    ]
    resources = [var.backups_bucket_arn]
  }

  statement {
    sid = "DatabaseBackupObjects"
    actions = [
      "s3:GetObject",
      "s3:PutObject"
    ]
    resources = ["${var.backups_bucket_arn}/*"]
  }

  statement {
    sid       = "CloudFrontInvalidation"
    actions   = ["cloudfront:CreateInvalidation", "cloudfront:GetInvalidation"]
    resources = [var.cloudfront_distribution_arn]
  }

  statement {
    sid = "LightsailDeploymentAccess"
    actions = [
      "lightsail:GetInstance",
      "lightsail:GetInstanceAccessDetails",
      "lightsail:PutInstancePublicPorts"
    ]
    resources = ["*"]
  }

  statement {
    sid = "ReadDeploymentParameters"
    actions = [
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParametersByPath"
    ]
    resources = ["${var.parameter_path_arn}*"]
  }
}

resource "aws_iam_role_policy" "deploy" {
  name   = "${var.name}-policy"
  role   = aws_iam_role.this.id
  policy = data.aws_iam_policy_document.deploy.json
}
