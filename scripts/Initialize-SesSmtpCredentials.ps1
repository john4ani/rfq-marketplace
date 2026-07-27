[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('staging', 'production')]
    [string] $EnvironmentName,

    [Parameter(Mandatory)]
    [string] $IamUserName,

    [string] $AwsRegion = 'eu-central-1'
)

$ErrorActionPreference = 'Stop'
$createdAccessKeyId = $null

function Invoke-HmacSha256 {
    param(
        [byte[]] $Key,
        [byte[]] $Data
    )

    $hmac = [Security.Cryptography.HMACSHA256]::new($Key)
    try {
        $hmac.ComputeHash($Data)
    }
    finally {
        $hmac.Dispose()
    }
}

function ConvertTo-SesSmtpPassword {
    param(
        [string] $SecretAccessKey,
        [string] $Region
    )

    $utf8 = [Text.Encoding]::UTF8
    $dateKey = Invoke-HmacSha256 ($utf8.GetBytes("AWS4$SecretAccessKey")) ($utf8.GetBytes('11111111'))
    $regionKey = Invoke-HmacSha256 $dateKey ($utf8.GetBytes($Region))
    $serviceKey = Invoke-HmacSha256 $regionKey ($utf8.GetBytes('ses'))
    $terminalKey = Invoke-HmacSha256 $serviceKey ($utf8.GetBytes('aws4_request'))
    $signature = Invoke-HmacSha256 $terminalKey ($utf8.GetBytes('SendRawEmail'))
    [Convert]::ToBase64String([byte[]]@(0x04) + $signature)
}

try {
    $existingKeys = & aws iam list-access-keys `
        --user-name $IamUserName `
        --query 'AccessKeyMetadata[].AccessKeyId' `
        --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect SMTP access keys for $IamUserName."
    }
    if (@($existingKeys).Count -ge 2) {
        throw "$IamUserName already has two access keys. Rotate one explicitly before creating another."
    }

    $created = & aws iam create-access-key `
        --user-name $IamUserName `
        --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to create an SMTP access key for $IamUserName."
    }

    $createdAccessKeyId = $created.AccessKey.AccessKeyId
    $smtpPassword = ConvertTo-SesSmtpPassword `
        -SecretAccessKey $created.AccessKey.SecretAccessKey `
        -Region $AwsRegion

    $secureUsername = ConvertTo-SecureString $createdAccessKeyId -AsPlainText -Force
    $securePassword = ConvertTo-SecureString $smtpPassword -AsPlainText -Force
    & (Join-Path $PSScriptRoot 'Set-DeploymentSecrets.ps1') `
        -EnvironmentName $EnvironmentName `
        -AwsRegion $AwsRegion `
        -SmtpUsername $secureUsername `
        -SmtpPassword $securePassword

    Write-Host "SES SMTP credentials were created and stored for $EnvironmentName. No credential values were printed."
}
catch {
    if ($createdAccessKeyId) {
        & aws iam delete-access-key `
            --user-name $IamUserName `
            --access-key-id $createdAccessKeyId | Out-Null
    }
    throw
}
