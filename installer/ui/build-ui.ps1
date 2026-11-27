$ErrorActionPreference = 'Stop'
$root = 'C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko'
$ui = Join-Path $root 'installer\ui'
$out = Join-Path $ui 'build'
New-Item -ItemType Directory -Force $out | Out-Null
$resDir = Join-Path $out 'res'
New-Item -ItemType Directory -Force $resDir | Out-Null
Copy-Item (Join-Path $ui 'assets\welcome.png') (Join-Path $resDir 'Page.Welcome.png') -Force
Copy-Item (Join-Path $ui 'assets\directory.png') (Join-Path $resDir 'Page.Directory.png') -Force
Copy-Item (Join-Path $ui 'assets\progress.png') (Join-Path $resDir 'Page.Progress.png') -Force
Copy-Item (Join-Path $ui 'assets\finish.png') (Join-Path $resDir 'Page.Finish.png') -Force
Copy-Item (Join-Path $ui 'assets\uninstall-confirm.png') (Join-Path $resDir 'Page.UninstallConfirm.png') -Force
Copy-Item (Join-Path $ui 'assets\uninstall-finish.png') (Join-Path $resDir 'Page.UninstallFinish.png') -Force
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'
$refs = @(
  (Join-Path $wpf 'PresentationFramework.dll'),
  (Join-Path $wpf 'PresentationCore.dll'),
  (Join-Path $wpf 'WindowsBase.dll'),
  (Join-Path $wpf 'PresentationFramework-SystemCore.dll'),
  (Join-Path $wpf 'PresentationFramework-SystemDrawing.dll'),
  (Join-Path $wpf 'PresentationFramework-SystemXml.dll'),
  (Join-Path $wpf 'PresentationFramework-SystemXmlLinq.dll'),
  'System.dll',
  'System.Core.dll',
  'System.Xaml.dll',
  'System.Windows.Forms.dll',
  'System.Drawing.dll',
  'System.IO.Compression.dll',
  'System.IO.Compression.FileSystem.dll',
  'Microsoft.CSharp.dll'
)
$refArgs = $refs | ForEach-Object { '/reference:' + $_ }
$icon = Join-Path $root 'src\MikoMiko\Assets\icon.ico'
$source = Join-Path $ui 'MikoMikoSetupUI.cs'
$uninstallerExe = Join-Path $out 'UninstallUI.exe'
$setupExe = Join-Path $out 'SetupUI.exe'

$pageResources = @(
  (Join-Path $out 'res\Page.Welcome.png'),
  (Join-Path $out 'res\Page.Directory.png'),
  (Join-Path $out 'res\Page.Progress.png'),
  (Join-Path $out 'res\Page.Finish.png'),
  (Join-Path $out 'res\Page.UninstallConfirm.png'),
  (Join-Path $out 'res\Page.UninstallFinish.png')
)
$pageArgs = $pageResources | ForEach-Object { '/resource:' + $_ }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /debug- /win32icon:$icon "/out:$uninstallerExe" $refArgs $source $pageArgs
if ($LASTEXITCODE -ne 0) { throw 'Uninstall UI compile failed' }

$setupResources = @($pageResources) + @($uninstallerExe)
$setupArgs = $setupResources | ForEach-Object { '/resource:' + $_ }
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /debug- /win32icon:$icon "/out:$setupExe" $refArgs $source $setupArgs
if ($LASTEXITCODE -ne 0) { throw 'Setup UI compile failed' }

Get-ChildItem $out | Select-Object Name, Length
