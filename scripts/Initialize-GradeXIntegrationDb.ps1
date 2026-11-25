param(
    [switch] $Drop
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$database = 'GradeX_IntegrationTests'

$repo = Split-Path -Parent $PSScriptRoot
$connectionString =
    "Server=(localdb)\MSSQLLocalDB;Database=$database;" +
    'Trusted_Connection=True;TrustServerCertificate=True'

Push-Location $repo
try {
    $localDb = (Get-Command sqllocaldb -ErrorAction SilentlyContinue).Source
    if (-not $localDb) {
        $localDb = Get-ChildItem 'C:\Program Files\Microsoft SQL Server' `
            -Recurse -Filter SqlLocalDB.exe -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $localDb -or -not (Test-Path -LiteralPath $localDb)) {
        throw 'No se encontró SqlLocalDB.exe en el runner.'
    }

    $instances = & $localDb info 2>$null
    if ($LASTEXITCODE -ne 0 -or -not ($instances -contains 'MSSQLLocalDB')) {
        & $localDb create MSSQLLocalDB | Out-Null
    }
    $instanceInfo = & $localDb info MSSQLLocalDB 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudo consultar la instancia MSSQLLocalDB.'
    }
    if (($instanceInfo | Out-String) -notmatch 'State:\s+Running') {
        & $localDb start MSSQLLocalDB | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'No se pudo iniciar MSSQLLocalDB.'
        }
    }

    $env:GRADEX_DESIGN_TIME_CONNECTION = $connectionString
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet tool restore falló.'
    }
    dotnet restore GradeX.slnx --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore falló.'
    }

    if ($Drop) {
        dotnet ef database drop `
            --force `
            --project src/GradeX.Infrastructure `
            --startup-project src/GradeX.Api `
            --configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw 'No se pudo eliminar la base de integración.'
        }
        Write-Host "Base $database eliminada."
        return
    }

    dotnet ef database update `
        --project src/GradeX.Infrastructure `
        --startup-project src/GradeX.Api `
        --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudieron aplicar las migraciones a la base de integración.'
    }

    $sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
    if (-not $sqlcmd) {
        $sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server' `
            -Recurse -Filter SQLCMD.EXE -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if ($sqlcmd) {
        $name = & $sqlcmd -S '(localdb)\MSSQLLocalDB' -d $database `
            -Q 'SET NOCOUNT ON; SELECT DB_NAME()' -h -1 -W -b
        if ($LASTEXITCODE -ne 0 -or (($name | Out-String).Trim() -ne $database)) {
            throw "La base efectiva no es $database."
        }
        Write-Host "LocalDB listo: MSSQLLocalDB/$database"
    }
    else {
        Write-Warning 'sqlcmd no está disponible; el test validará el destino efectivo.'
    }
}
finally {
    Pop-Location
}
