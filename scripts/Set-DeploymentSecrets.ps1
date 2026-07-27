[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('staging', 'production')]
    [string] $EnvironmentName,

    [string] $AwsRegion = 'eu-central-1',

    [SecureString] $PostgresPassword,

    [SecureString] $JwtSigningKey,

    [Parameter(Mandatory)]
    [SecureString] $SmtpUsername,

    [Parameter(Mandatory)]
    [SecureString] $SmtpPassword
)

$ErrorActionPreference = 'Stop'
$parameterRoot = "/rfq-marketplace/$EnvironmentName"

function New-RandomSecret {
    param([int] $Length = 48)

    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = [byte[]]::new($Length)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

function ConvertTo-PlainText {
    param([SecureString] $Value)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Set-SecureParameter {
    param(
        [string] $Name,
        [string] $Value
    )

    $temporaryFile = [IO.Path]::GetTempFileName()
    try {
        $payload = @{
            Name      = "$parameterRoot/$Name"
            Type      = 'SecureString'
            Value     = $Value
            Overwrite = $true
        } | ConvertTo-Json -Compress

        [IO.File]::WriteAllText($temporaryFile, $payload, [Text.UTF8Encoding]::new($false))
        & aws ssm put-parameter --region $AwsRegion --cli-input-json "file://$temporaryFile" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to store $parameterRoot/$Name."
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}

if (-not $PostgresPassword) {
    $PostgresPassword = ConvertTo-SecureString (New-RandomSecret 48) -AsPlainText -Force
}
if (-not $JwtSigningKey) {
    $JwtSigningKey = ConvertTo-SecureString (New-RandomSecret 64) -AsPlainText -Force
}

Set-SecureParameter 'postgres-password' (ConvertTo-PlainText $PostgresPassword)
Set-SecureParameter 'jwt-signing-key' (ConvertTo-PlainText $JwtSigningKey)
Set-SecureParameter 'smtp-username' (ConvertTo-PlainText $SmtpUsername)
Set-SecureParameter 'smtp-password' (ConvertTo-PlainText $SmtpPassword)

Write-Host "Deployment secrets are configured under $parameterRoot. No secret values were printed."
