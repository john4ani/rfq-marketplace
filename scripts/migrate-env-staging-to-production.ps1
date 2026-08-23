[CmdletBinding()]
param(
    [string] $AwsRegion = 'eu-central-1',
    [string] $SesSmtpUserName = 'rfq-marketplace-production-ses-smtp'
)

# Migrates SSM parameters from the legacy /rfq-marketplace/staging/* path to /rfq-marketplace/production/*
# after the clean production recreation (2026-08-23). The old SMTP credentials belonged to the destroyed
# 'rfq-marketplace-staging-ses-smtp' IAM user, so they are NOT copied — new SES SMTP credentials are created
# for the production user instead and stored under both email-username/email-password (what
# render-environment.sh reads) and smtp-username/smtp-password (what the PowerShell scripts write).
# Finally, the legacy /staging/* parameters are deleted, leaving the old path empty.

$ErrorActionPreference = 'Stop'
$sourceRoot = '/rfq-marketplace/staging'
$targetRoot = '/rfq-marketplace/production'
$skippedNames = @('smtp-username', 'smtp-password')

function ConvertTo-SesSmtpPassword {
    param(
        [string] $SecretAccessKey,
        [string] $Region
    )
    $utf8 = [Text.Encoding]::UTF8
    function Invoke-HmacSha256([byte[]] $Key, [byte[]] $Value) {
        $hmac = [Security.Cryptography.HMACSHA256]::new($Key)
        try { $hmac.ComputeHash($Value) } finally { $hmac.Dispose() }
    }
    $dateKey = Invoke-HmacSha256 ($utf8.GetBytes("AWS4$SecretAccessKey")) ($utf8.GetBytes('11111111'))
    $regionKey = Invoke-HmacSha256 $dateKey ($utf8.GetBytes($Region))
    $serviceKey = Invoke-HmacSha256 $regionKey ($utf8.GetBytes('ses'))
    $terminalKey = Invoke-HmacSha256 $serviceKey ($utf8.GetBytes('aws4_request'))
    $signature = Invoke-HmacSha256 $terminalKey ($utf8.GetBytes('SendRawEmail'))
    [Convert]::ToBase64String([byte[]]@(0x04) + $signature)
}

function Set-SsmParameter {
    param([string] $Name, [string] $Type, [string] $Value)
    $payload = @{ Name = $Name; Type = $Type; Value = $Value; Overwrite = $true } | ConvertTo-Json -Compress
    $temporaryFile = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText($temporaryFile, $payload, [Text.UTF8Encoding]::new($false))
        & aws ssm put-parameter --region $AwsRegion --cli-input-json "file://$($temporaryFile.Replace('\', '/'))" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to write $Name." }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile) { Remove-Item -LiteralPath $temporaryFile -Force }
    }
}

Write-Host "Reading parameters from $sourceRoot ..."
$items = & aws ssm get-parameters-by-path --path $sourceRoot --recursive --with-decryption --query 'Parameters[].[Name,Type,Value]' --output json --region $AwsRegion | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "Failed to read parameters under $sourceRoot." }
if (-not $items -or @($items).Count -eq 0) { Write-Host 'No parameters to migrate.'; return }

$copied = 0
foreach ($parameter in @($items)) {
    $shortName = $parameter[0].Substring($sourceRoot.Length + 1)
    if ($shortName -in $skippedNames) { Write-Host "skipping $shortName (old SES SMTP credentials are invalid)"; continue }
    Set-SsmParameter -Name "$targetRoot/$shortName" -Type $parameter[1] -Value $parameter[2]
    $copied++
}
Write-Host "Copied $copied parameters from $sourceRoot to $targetRoot."

Write-Host "Creating fresh SES SMTP credentials for $SesSmtpUserName ..."
$existingKeys = @(& aws iam list-access-keys --user-name $SesSmtpUserName --query 'AccessKeyMetadata[].AccessKeyId' --output json --region $AwsRegion | ConvertFrom-Json)
if ($existingKeys.Count -ge 2) { throw "$SesSmtpUserName already has two access keys; rotate one explicitly first." }
$created = & aws iam create-access-key --user-name $SesSmtpUserName --output json --region $AwsRegion | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Failed to create an SMTP access key.' }
$accessKeyId = $created.AccessKey.AccessKeyId
$smtpPassword = ConvertTo-SesSmtpPassword -SecretAccessKey $created.AccessKey.SecretAccessKey -Region $AwsRegion

foreach ($name in @('email-username', 'email-password', 'smtp-username', 'smtp-password')) {
    $value = if ($name -like '*password*') { $smtpPassword } else { $accessKeyId }
    Set-SsmParameter -Name "$targetRoot/$name" -Type 'SecureString' -Value $value
}
Write-Host "Stored fresh SES SMTP credentials under $targetRoot/email-* and smtp-*. No values printed."

Write-Host "Deleting parameters from $sourceRoot ..."
$names = @($items | ForEach-Object { $_[0] })
for ($i = 0; $i -lt $names.Count; $i += 10) {
    $chunk = $names[$i..[Math]::Min($i + 9, $names.Count - 1)]
    & aws ssm delete-parameters --region $AwsRegion --names $chunk | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to delete a batch of staging parameters.' }
}
Write-Host "Deleted $($names.Count) parameters from $sourceRoot (legacy path left empty)."
