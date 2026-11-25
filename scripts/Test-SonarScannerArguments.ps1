param(
    [string] $WorkflowPath = '.github/workflows/sonar.yml'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$path = (Resolve-Path -LiteralPath $WorkflowPath).Path
$content = Get-Content -LiteralPath $path -Raw

$beginPattern = '(?s)- name:\s*Begin SonarCloud analysis.*?(?=\r?\n\s*- name:\s*Build instrumented by SonarScanner)'
$endPattern = '(?s)- name:\s*End SonarCloud analysis.*?(?=\r?\n\s*- name:\s*Upload coverage and test results)'

$begin = [regex]::Match($content, $beginPattern)
$end = [regex]::Match($content, $endPattern)
if (-not $begin.Success) { throw 'No se encontró el bloque Begin SonarCloud analysis.' }
if (-not $end.Success) { throw 'No se encontró el bloque End SonarCloud analysis.' }

$requiredBegin = @(
    '-k:"Jessui630_GradeX-back"',
    '-o:"jessui630"',
    '-d:sonar.host.url="https://sonarcloud.io"',
    '-d:sonar.cs.opencover.reportsPaths="coverage/sonar/**/coverage.opencover.xml"',
    '-d:sonar.token="$SONAR_TOKEN"'
)
foreach ($argument in $requiredBegin) {
    if (-not $begin.Value.Contains($argument)) {
        throw "Falta el argumento esperado en begin: $argument"
    }
}
if ($begin.Value -match '(?m)^\s*/[kod]:') {
    throw 'Begin usa la sintaxis /k:, /o: o /d:, que Git Bash convierte a rutas.'
}
if ($begin.Value -notmatch '(?m)^\s*shell:\s*bash\s*$') {
    throw 'Begin debe ejecutarse con shell bash para validar la compatibilidad Git Bash.'
}

if (-not $end.Value.Contains('-d:sonar.token="$env:SONAR_TOKEN"')) {
    throw 'End no usa -d:sonar.token con la credencial temporal.'
}
if ($end.Value -match '(?m)^\s*/[kod]:') {
    throw 'End usa la sintaxis /d:, que Git Bash convierte a rutas.'
}

Write-Host 'SonarScanner usa -k:, -o: y -d: en begin/end; no hay argumentos /k:, /o: ni /d:.'
