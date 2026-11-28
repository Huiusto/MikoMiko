$ErrorActionPreference = 'Stop'
$root = 'C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko'
Set-Location $root
& (Join-Path $root 'installer\ui\build-ui.ps1')
if ($LASTEXITCODE -ne 0) { throw 'UI build failed' }

$appDir = Join-Path $root 'dist\app'
$payload = Join-Path $root 'dist\payload-0164.zip'
if (Test-Path $payload) { Remove-Item $payload -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($appDir, $payload, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$setupUi = Join-Path $root 'installer\ui\build\SetupUI.exe'
$output = Join-Path $root 'dist\MikoMiko-Setup-0.1.64.exe'
if (Test-Path $output) { Remove-Item $output -Force }

$input = [System.IO.File]::OpenRead($setupUi)
$out = [System.IO.File]::Create($output)
try {
    $input.CopyTo($out)
    $input.Dispose()
    $payloadStream = [System.IO.File]::OpenRead($payload)
    try { $payloadStream.CopyTo($out) } finally { $payloadStream.Dispose() }
    $marker = [System.Text.Encoding]::ASCII.GetBytes('MIKOPAY1')
    $out.Write($marker, 0, 8)
    $size = (Get-Item $payload).Length
    $sizeBytes = [BitConverter]::GetBytes([int64]$size)
    $out.Write($sizeBytes, 0, 8)
} finally {
    $out.Dispose()
    if ($input) { $input.Dispose() }
}

Get-Item $output | Select-Object Name, Length, LastWriteTime


