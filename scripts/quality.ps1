[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,
    [switch] $FailOnWarnings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $output) {
    throw "Evidence directory already exists; choose a new path: $output"
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sarifDirectory = Join-Path $output 'sarif'
New-Item -ItemType Directory -Path $sarifDirectory | Out-Null
$commands = [System.Collections.Generic.List[object]]::new()

function Invoke-DotnetLogged {
    param([string] $Name, [string[]] $CommandArguments)
    $started = [DateTime]::UtcNow.ToString('o')
    $log = Join-Path $output "$Name.log"
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & dotnet @CommandArguments 2>&1 | Out-File -FilePath $log -Encoding utf8
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    $commands.Add([pscustomobject]@{
        command = 'dotnet ' + ($CommandArguments -join ' ')
        arguments = $CommandArguments
        startedUtc = $started
        finishedUtc = [DateTime]::UtcNow.ToString('o')
        exitCode = $code
        log = "$Name.log"
    })
    Write-Host "$Name exit=$code ($log)"
    return $code
}

$oldLanguage = $env:DOTNET_CLI_UI_LANGUAGE
$env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
Push-Location $root
try {
    $hashes = @('global.json', '.editorconfig', 'Directory.Build.props', 'scripts/quality.ps1') |
        ForEach-Object {
            [pscustomobject]@{ path = $_; sha256 = (Get-FileHash $_ -Algorithm SHA256).Hash }
        }
    $sdkCode = Invoke-DotnetLogged 'sdk' @('--info')
    $restoreCode = Invoke-DotnetLogged 'restore' @('restore', 'GradeX.slnx', '--nologo')
    $buildCode = $null
    $testCode = $null
    $auditCode = $null
    $auditVulnerable = $false
    if ($sdkCode -eq 0 -and $restoreCode -eq 0) {
        $buildCode = Invoke-DotnetLogged 'analysis' @(
            'build', 'GradeX.slnx', '--no-restore', '--configuration', 'Release',
            '-t:Rebuild', '--nologo', '-p:QualityAnalysis=true',
            "-p:QualityOutputDirectory=$sarifDirectory"
        )
        if ($buildCode -eq 0) {
            $auditCode = Invoke-DotnetLogged 'audit' @(
                'list', 'GradeX.slnx', 'package', '--vulnerable', '--include-transitive'
            )
            $auditLog = Join-Path $output 'audit.log'
            if (Test-Path $auditLog) {
                $auditVulnerable = [bool](Select-String -Path $auditLog `
                    -Pattern 'has the following vulnerable packages' -Quiet)
            }
            $testCode = Invoke-DotnetLogged 'tests' @(
                'test', 'GradeX.slnx', '--no-build', '--no-restore', '--configuration', 'Release',
                '--nologo', '--logger', 'trx;LogFileName=tests.trx',
                '--collect:XPlat Code Coverage',
                '--results-directory', (Join-Path $output 'tests')
            )
        }
    }

    $diagnostics = @(
        foreach ($file in Get-ChildItem $sarifDirectory -Filter '*.sarif') {
            $sarif = Get-Content $file.FullName -Raw | ConvertFrom-Json
            foreach ($run in $sarif.runs) {
                foreach ($result in $run.results) {
                    $uri = $null
                    $line = $null
                    if ($result.PSObject.Properties.Name -contains 'locations' -and $result.locations.Count -gt 0) {
                        $physical = $result.locations[0].physicalLocation
                        $uri = $physical.artifactLocation.uri
                        if ($physical.PSObject.Properties.Name -contains 'region') {
                            $line = $physical.region.startLine
                        }
                    }
                    [pscustomobject]@{
                        project = $file.BaseName; rule = $result.ruleId; level = $result.level
                        file = $uri; line = $line; message = $result.message.text
                        suppressed = ($result.PSObject.Properties.Name -contains 'suppressions' -and $result.suppressions.Count -gt 0)
                    }
                }
            }
        }
    )
    $sarifCount = @(Get-ChildItem $sarifDirectory -Filter '*.sarif').Count
    $expectedProjects = @('GradeX.Domain', 'GradeX.Application', 'GradeX.Infrastructure', 'GradeX.Api', 'GradeX.Tests')
    $missingProjects = @($expectedProjects | Where-Object { -not (Test-Path (Join-Path $sarifDirectory "$_.sarif")) })
    $warnings = @($diagnostics | Where-Object { $_.level -eq 'warning' -and -not $_.suppressed }).Count
    $errors = @($diagnostics | Where-Object { $_.level -eq 'error' -and -not $_.suppressed }).Count
    $coverageReports = @(Get-ChildItem (Join-Path $output 'tests') `
        -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName })
    $coverage = $null
    if ($coverageReports.Count -gt 0) {
        try {
            [xml] $cobertura = Get-Content $coverageReports[0] -Raw
            $coverageRoot = $cobertura.coverage
            $coverage = [pscustomobject]@{
                report = 'coverage.cobertura.xml'
                lineRate = [double]::Parse($coverageRoot.'line-rate',
                    [Globalization.CultureInfo]::InvariantCulture)
                branchRate = [double]::Parse($coverageRoot.'branch-rate',
                    [Globalization.CultureInfo]::InvariantCulture)
                linesCovered = [int] $coverageRoot.'lines-covered'
                linesValid = [int] $coverageRoot.'lines-valid'
                branchesCovered = [int] $coverageRoot.'branches-covered'
                branchesValid = [int] $coverageRoot.'branches-valid'
            }
        }
        catch {
            $coverage = [pscustomobject]@{
                report = 'coverage.cobertura.xml'
                error = $_.Exception.Message
            }
        }
    }
    $buildLogWarnings = 0
    foreach ($logName in @('restore.log', 'analysis.log')) {
        $logPath = Join-Path $output $logName
        if (Test-Path $logPath) {
            $buildLogWarnings += @(Select-String -Path $logPath -Pattern '\bwarning (?:[A-Z]+\d+|:)').Count
        }
    }
    $success = $sdkCode -eq 0 -and $restoreCode -eq 0 -and $buildCode -eq 0 -and
        $testCode -eq 0 -and $auditCode -eq 0 -and -not $auditVulnerable -and
        $coverageReports.Count -gt 0 -and $missingProjects.Count -eq 0 -and $errors -eq 0
    if ($FailOnWarnings -and ($warnings -gt 0 -or $buildLogWarnings -gt 0)) { $success = $false }
    $summary = [pscustomobject]@{
        finishedUtc = [DateTime]::UtcNow.ToString('o')
        configuration = 'Release'; analysisLevel = '10.0'; analysisMode = 'Recommended'
        failOnWarnings = [bool]$FailOnWarnings
        rulesAndRunner = @($hashes); commands = @($commands.ToArray())
        sarifReports = $sarifCount; missingProjects = @($missingProjects)
        warnings = $warnings; errors = $errors
        suppressedDiagnostics = @($diagnostics | Where-Object { $_.suppressed }).Count
        notes = @($diagnostics | Where-Object { $_.level -eq 'note' -and -not $_.suppressed }).Count
        logWarningLinesIncludingBuildSummaryDuplicates = $buildLogWarnings
        coverageReports = @($coverageReports)
        coverage = $coverage
        dependencyAudit = [pscustomobject]@{
            exitCode = $auditCode; vulnerable = $auditVulnerable; log = 'audit.log'
        }
        diagnostics = @($diagnostics); success = $success
    }
    $summary | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
    Write-Host "SARIF reports=$sarifCount warnings=$warnings errors=$errors success=$success"
    if (-not $success) { throw "Quality gate failed; inspect $output" }
}
finally {
    Pop-Location
    $env:DOTNET_CLI_UI_LANGUAGE = $oldLanguage
}
