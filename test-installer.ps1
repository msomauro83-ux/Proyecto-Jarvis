param([switch]$VerificationBuild)
$ErrorActionPreference = 'Stop'
$testDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build\installer-verification-0.4.4'))
$expectedDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $testDirectory.StartsWith($expectedDirectory, [StringComparison]::OrdinalIgnoreCase)) { throw 'La carpeta de prueba debe estar dentro de build.' }
if (Test-Path -LiteralPath $testDirectory) { throw 'La carpeta de prueba ya existe. Revisa su contenido antes de repetir la prueba.' }
$applicationId = if ($VerificationBuild) { '{7B479844-6D3D-45A8-ACCB-A68156303A4D}' } else { '{0A59645C-4D26-4BFB-911C-0C8D88DD3346}' }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $applicationId + '_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'Hay una instalacion registrada de Jarvis. La prueba no la modificara.' }
$installerName = if ($VerificationBuild) { 'artifacts\Jarvis-Vale-Verification-0.4.4.exe' } else { 'artifacts\Jarvis-Vale-Setup-0.4.4.exe' }
$installer = Join-Path $PSScriptRoot $installerName
if (-not (Test-Path -LiteralPath $installer)) { throw 'Primero compila el instalador correspondiente.' }
function Invoke-VerificationProcess([string]$Executable, [string[]]$Arguments) {
    $verificationProcess = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $verificationProcess.WaitForExit(30000)) { throw 'La operacion de instalacion de prueba sigue abierta. Revisa los registros en build.' }
    if ($verificationProcess.ExitCode -ne 0) { throw ('La operacion de prueba termino con codigo ' + $verificationProcess.ExitCode) }
}
function Get-StartupCommand {
    $startupRegistry = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    if ($null -eq $startupRegistry) { return $null }
    try { return $startupRegistry.GetValue('JarvisVale', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
    finally { $startupRegistry.Dispose() }
}
$startupBefore = Get-StartupCommand
function Assert-StartupUnchanged {
    if (-not [String]::Equals($startupBefore, (Get-StartupCommand), [StringComparison]::Ordinal)) { throw 'La prueba modifico el inicio con Windows existente.' }
}
function Assert-TestRegistration {
    $installedRegistration = Get-ItemProperty -LiteralPath $uninstallKey -ErrorAction Stop
    if ([String]::IsNullOrWhiteSpace($installedRegistration.InstallLocation)) { throw 'La instalacion de prueba no tiene una ruta registrada.' }
    $registeredDirectory = [System.IO.Path]::GetFullPath($installedRegistration.InstallLocation).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if (-not [String]::Equals($registeredDirectory, $testDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) { throw 'La ruta registrada no coincide con la carpeta aislada de prueba. No se desinstalara.' }
}
$installArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', ('/DIR="' + $testDirectory + '"'))
Invoke-VerificationProcess $installer ($installArguments + ('/LOG="' + (Join-Path $PSScriptRoot 'build\installer-install.log') + '"'))
Assert-StartupUnchanged
Assert-TestRegistration
$shortcutName = if ($VerificationBuild) { 'Jarvis - Vale Verification 0.4.4\Jarvis - Vale.lnk' } else { 'Jarvis - Vale\Jarvis - Vale.lnk' }
$testShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) $shortcutName
if (Test-Path -LiteralPath $testShortcut) { throw 'La prueba solicito no crear accesos directos, pero se creo uno.' }
$installedExecutable = Join-Path $testDirectory 'Jarvis.exe'
foreach ($distributionFile in @('Jarvis.exe', 'Jarvis.exe.config')) {
    if ((Get-FileHash -LiteralPath (Join-Path $testDirectory $distributionFile)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('dist\' + $distributionFile))).Hash) { throw ('El archivo instalado no coincide con la distribucion: ' + $distributionFile) }
}
if ((Get-FileHash -LiteralPath (Join-Path $testDirectory 'LEEME.md')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'README.md')).Hash) { throw 'La guia instalada no coincide con la distribucion.' }
$obsoleteFiles = @('Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll', 'WebView2-LICENSE.txt', 'WebView2-NOTICE.txt', 'docs\CHATGPT.md')
foreach ($obsoleteFile in $obsoleteFiles) {
    if (Test-Path -LiteralPath (Join-Path $testDirectory $obsoleteFile)) { throw ('La instalacion incluyo un archivo de una funcion retirada: ' + $obsoleteFile) }
}
if (Test-Path -LiteralPath (Join-Path $testDirectory 'data')) { throw 'El instalador incluyo ajustes personales.' }
Invoke-VerificationProcess $installedExecutable @('--smoke-test')
$testData = Join-Path $testDirectory 'data'
$testDocs = Join-Path $testDirectory 'docs'
New-Item -ItemType Directory -Path $testData, $testDocs | Out-Null
$testSettings = Join-Path $testData 'settings.json'
$sentinel = '{"UserName":"Prueba de instalador","SpeakLocal":false}'
[System.IO.File]::WriteAllText($testSettings, $sentinel)
$unrelatedFiles = @((Join-Path $testData 'unrelated-verification.txt'), (Join-Path $testDocs 'unrelated-verification.txt'))
foreach ($unrelatedFile in $unrelatedFiles) { [System.IO.File]::WriteAllText($unrelatedFile, 'keep verification file') }
# Simulate only known files shipped by earlier releases. Upgrade must remove
# these exact files and preserve local settings, unrelated files and startup.
foreach ($obsoleteFile in $obsoleteFiles) { [System.IO.File]::WriteAllText((Join-Path $testDirectory $obsoleteFile), 'obsolete verification file') }
Invoke-VerificationProcess $installer ($installArguments + ('/LOG="' + (Join-Path $PSScriptRoot 'build\installer-upgrade.log') + '"'))
Assert-StartupUnchanged
Assert-TestRegistration
if ([System.IO.File]::ReadAllText($testSettings) -ne $sentinel) { throw 'La actualizacion no conservo los ajustes locales.' }
foreach ($unrelatedFile in $unrelatedFiles) { if ([System.IO.File]::ReadAllText($unrelatedFile) -ne 'keep verification file') { throw 'La actualizacion modifico un archivo ajeno.' } }
foreach ($obsoleteFile in $obsoleteFiles) { if (Test-Path -LiteralPath (Join-Path $testDirectory $obsoleteFile)) { throw ('La actualizacion no quito un archivo obsoleto: ' + $obsoleteFile) } }
Invoke-VerificationProcess (Join-Path $testDirectory 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $PSScriptRoot 'build\installer-uninstall.log') + '"'))
Assert-StartupUnchanged
if (Test-Path -LiteralPath $installedExecutable) { throw 'La desinstalacion no quito el ejecutable de prueba.' }
if (Test-Path -LiteralPath $uninstallKey) { throw 'La desinstalacion no quito el registro de la instalacion de prueba.' }
if ([System.IO.File]::ReadAllText($testSettings) -ne $sentinel) { throw 'La desinstalacion no conservo los ajustes de prueba.' }
foreach ($unrelatedFile in $unrelatedFiles) { if ([System.IO.File]::ReadAllText($unrelatedFile) -ne 'keep verification file') { throw 'La desinstalacion modifico un archivo ajeno.' } }
# Remove only dummy files created by this test, then their empty directories.
Remove-Item -LiteralPath $testSettings
foreach ($unrelatedFile in $unrelatedFiles) { Remove-Item -LiteralPath $unrelatedFile }
Remove-Item -LiteralPath $testData
Remove-Item -LiteralPath $testDocs
Write-Output 'OK: instalacion local, guia, ejecutable, actualizacion y desinstalacion. Ajustes e inicio con Windows conservados; archivos antiguos retirados; sin accesos directos ni microfono.'
