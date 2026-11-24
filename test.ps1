$ErrorActionPreference = 'Stop'
$verificationDirectory = Join-Path $PSScriptRoot 'build\verification'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $verificationDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testOutput = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$testExecutable = Join-Path $testOutput 'CommandTests.exe'
$testSources = @((Join-Path $PSScriptRoot 'src\Commands.cs'), (Join-Path $PSScriptRoot 'src\Core.cs'), (Join-Path $PSScriptRoot 'src\SpeechGrammar.cs'), (Join-Path $PSScriptRoot 'tests\CommandTests.cs'))
$testSources += @((Join-Path $PSScriptRoot 'src\AppCatalog.cs'), (Join-Path $PSScriptRoot 'src\LocalSpeech.cs'), (Join-Path $PSScriptRoot 'tests\AppCatalogTests.cs'))
$speechAssembly = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter System.Speech.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
$testArguments = @('/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', ('/out:' + $testExecutable), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll')
$testArguments += ('/reference:' + $speechAssembly)
& $compiler @testArguments @testSources
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de comandos.' }
$smokeProcess = Start-Process -FilePath (Join-Path $verificationDirectory 'Jarvis.exe') -ArgumentList '--smoke-test' -PassThru -WindowStyle Hidden
if (-not $smokeProcess.WaitForExit(20000)) { $smokeProcess.Kill(); throw 'La prueba de interfaz excedio el tiempo limite.' }
if ($smokeProcess.ExitCode -ne 0) { throw 'Fallo la prueba de interfaz.' }
Write-Host 'OK: interfaz creada y cerrada sin activar el microfono.'
