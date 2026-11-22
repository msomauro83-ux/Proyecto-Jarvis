param([switch]$Run, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Se requiere .NET Framework 4.8 en Windows.' }
$speechAssembly = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter System.Speech.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $speechAssembly) { throw 'No se encontro System.Speech. Activa .NET Framework 4.8 en Windows.' }
$buildOutputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $projectRoot 'dist' } else { [System.IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Path $buildOutputDirectory -Force | Out-Null
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$compilerArguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', '/codepage:65001', '/warn:4', ('/out:' + (Join-Path $buildOutputDirectory 'Jarvis.exe')), ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll', ('/reference:' + $speechAssembly))
if (Test-Path -LiteralPath (Join-Path $projectRoot 'assets\jarvis.ico')) { $compilerArguments += ('/win32icon:' + (Join-Path $projectRoot 'assets\jarvis.ico')) }
& $compiler @compilerArguments @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'La compilacion de Jarvis fallo.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'Jarvis.exe.config') -Destination (Join-Path $buildOutputDirectory 'Jarvis.exe.config') -Force
Write-Host ('Listo: ' + (Join-Path $buildOutputDirectory 'Jarvis.exe'))
if ($Run) { Start-Process -FilePath (Join-Path $buildOutputDirectory 'Jarvis.exe') -WorkingDirectory $buildOutputDirectory }
