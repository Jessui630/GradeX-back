[CmdletBinding()]
param(
    [string] $OutputPath = 'artifacts/migrations/GradeX_DevCloud.sql'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Dotnet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') falló con código $LASTEXITCODE."
    }
}

$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $root $OutputPath))
}
$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

Push-Location $root
try {
    Invoke-Dotnet @('tool', 'restore')
    Invoke-Dotnet @('build', 'GradeX.slnx', '--configuration', 'Release', '--nologo')

    if (Test-Path $resolvedOutput) {
        Remove-Item -LiteralPath $resolvedOutput -Force
    }

    Invoke-Dotnet @(
        'ef', 'migrations', 'script',
        '--idempotent',
        '--project', 'src/GradeX.Infrastructure',
        '--startup-project', 'src/GradeX.Api',
        '--context', 'GradeXDbContext',
        '--output', $resolvedOutput
    )

    if (-not (Test-Path $resolvedOutput)) {
        throw "No se generó el script: $resolvedOutput"
    }

    $file = Get-Item -LiteralPath $resolvedOutput
    if ($file.Length -eq 0) {
        throw "El script generado está vacío: $resolvedOutput"
    }

    $content = Get-Content -LiteralPath $resolvedOutput -Raw
    if ($content -notmatch '__EFMigrationsHistory') {
        throw 'El script no contiene el historial idempotente de EF Core.'
    }

    $hash = (Get-FileHash -LiteralPath $resolvedOutput -Algorithm SHA256).Hash
    Write-Output "Script idempotente generado: $resolvedOutput"
    Write-Output "Tamaño: $($file.Length) bytes"
    Write-Output "SHA256: $hash"
    Write-Output 'No se aplicó ninguna migración ni se abrió una conexión de base de datos.'
}
finally {
    Pop-Location
}
