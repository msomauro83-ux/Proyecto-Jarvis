param([string]$CompilerPath)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    $compilerCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
    )
    $CompilerPath = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'No se encontró Inno Setup. Instálalo o indica -CompilerPath con la ruta de ISCC.exe.'
}
& $CompilerPath (Join-Path $PSScriptRoot 'installer\Jarvis.iss')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el instalador.' }
Write-Host ('Instalador listo: ' + (Join-Path $PSScriptRoot 'artifacts\Jarvis-Vale-Setup-0.4.4.exe'))
