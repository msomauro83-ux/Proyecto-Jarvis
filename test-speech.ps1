$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$speechAssembly = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter System.Speech.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'build') -Force | Out-Null
$testExecutable = Join-Path $PSScriptRoot 'build\SpeechIntegrationTests.exe'
$sources = @((Join-Path $PSScriptRoot 'src\Commands.cs'), (Join-Path $PSScriptRoot 'src\SpeechGrammar.cs'), (Join-Path $PSScriptRoot 'tests\SpeechIntegrationTests.cs'))
$sources += (Join-Path $PSScriptRoot 'src\AppCatalog.cs')
$arguments = @('/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', ('/out:' + $testExecutable), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', ('/reference:' + $speechAssembly))
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'No compilaron las pruebas del motor de voz.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del motor de voz.' }
