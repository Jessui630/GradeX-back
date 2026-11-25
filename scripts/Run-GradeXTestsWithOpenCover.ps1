param(
    [string] $ResultsDirectory = 'coverage/sonar',
    [int] $MinimumPassed = 126
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $env:GRADEX_INTEGRATION_TEST_CONNECTION =
        'Server=(localdb)\MSSQLLocalDB;Database=GradeX_IntegrationTests;' +
        'Trusted_Connection=True;TrustServerCertificate=True'

    if (Test-Path -LiteralPath $ResultsDirectory) {
        Remove-Item -LiteralPath $ResultsDirectory -Recurse -Force
    }

    dotnet test GradeX.slnx `
        --configuration Release `
        --no-build `
        --nologo `
        --settings tests/GradeX.Tests/coverage.opencover.runsettings `
        --collect:"XPlat Code Coverage" `
        --results-directory $ResultsDirectory `
        --logger "trx;LogFileName=tests.trx"
    if ($LASTEXITCODE -ne 0) {
        throw 'La suite de pruebas falló.'
    }

    $trx = Get-ChildItem $ResultsDirectory -Recurse -Filter tests.trx |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $trx) {
        throw 'No se generó el archivo TRX.'
    }
    [xml]$testResults = Get-Content -LiteralPath $trx.FullName -Raw
    $results = @($testResults.GetElementsByTagName('UnitTestResult'))
    $passed = @($results | Where-Object { $_.outcome -eq 'Passed' }).Count
    $skipped = @($results | Where-Object { $_.outcome -eq 'NotExecuted' })
    $unexpected = @($skipped | Where-Object {
        $_.testName -notmatch 'PwnedPasswordsLiveTests'
    })
    if ($unexpected.Count -gt 0) {
        $names = $unexpected | ForEach-Object { $_.testName }
        throw "Pruebas omitidas inesperadamente: $($names -join ', ')"
    }
    if ($passed -lt $MinimumPassed) {
        throw "Solo $passed pruebas correctas; se esperaban al menos $MinimumPassed con SQL."
    }
    Write-Host "SQL real: $passed correctas, $($skipped.Count) omitida(s) externas."

    $report = Get-ChildItem $ResultsDirectory -Recurse `
        -Filter coverage.opencover.xml -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $report) {
        throw 'No se generó coverage.opencover.xml.'
    }
    [xml]$coverage = Get-Content -LiteralPath $report.FullName -Raw
    if ($coverage.DocumentElement.LocalName -ne 'CoverageSession') {
        throw "Raíz XML inesperada: $($coverage.DocumentElement.LocalName)"
    }
    $summary = $coverage.SelectSingleNode('/CoverageSession/Summary')
    if (-not $summary) {
        throw 'Falta Summary en el reporte OpenCover.'
    }
    $total = [int]$summary.numSequencePoints
    $visited = [int]$summary.visitedSequencePoints
    $points = @($coverage.SelectNodes('//SequencePoint')).Count
    if ($total -le 0 -or $visited -le 0 -or $points -le 0) {
        throw 'El reporte no contiene cobertura real.'
    }

    $checked = 0
    foreach ($file in @($coverage.SelectNodes('//File'))) {
        $full = [string]$file.fullPath
        if ([string]::IsNullOrWhiteSpace($full)) { continue }
        $relative = [System.IO.Path]::GetRelativePath($repo, $full)
        if ($relative.StartsWith('..')) { continue }
        if ($relative -match '(^|[\\/])(obj|bin)([\\/]|$)') { continue }
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw "Falta el archivo fuente: $relative"
        }
        $checked++
    }
    if ($checked -le 0) {
        throw 'El reporte no referencia archivos fuente del repositorio.'
    }
    Write-Host "OpenCover válido: $visited/$total puntos de secuencia, $points entradas, $checked archivos fuente"
    Write-Host "Reporte: $($report.FullName)"
}
finally {
    Pop-Location
}
