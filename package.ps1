param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packagePath = Join-Path $PSScriptRoot 'Jarvis-pre-release-0.4.3.zip'
$packageFiles = @(
    @{ Source = 'dist\Jarvis.exe'; Entry = 'dist/Jarvis.exe' },
    @{ Source = 'dist\Jarvis.exe.config'; Entry = 'dist/Jarvis.exe.config' },
    @{ Source = 'Iniciar Jarvis.cmd'; Entry = 'Iniciar Jarvis.cmd' },
    @{ Source = 'README.md'; Entry = 'README.md' }
)
# Only explicitly listed files are included; never package user settings.
$archiveStream = [System.IO.File]::Open($packagePath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($archiveStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($packageFile in $packageFiles) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $PSScriptRoot $packageFile.Source), $packageFile.Entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
} finally { $archiveStream.Dispose() }
Write-Host ('Paquete listo: ' + $packagePath)
