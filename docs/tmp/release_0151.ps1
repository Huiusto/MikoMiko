$ErrorActionPreference = 'Stop'
Set-Location 'C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

[StructLayout(LayoutKind.Sequential)]
public struct CREDENTIAL
{
    public uint Flags;
    public uint Type;
    public IntPtr TargetName;
    public IntPtr Comment;
    public FILETIME LastWritten;
    public uint CredentialBlobSize;
    public IntPtr CredentialBlob;
    public uint Persist;
    public uint AttributeCount;
    public IntPtr Attributes;
    public IntPtr TargetAlias;
    public IntPtr UserName;
}

public static class CredApi
{
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern void CredFree(IntPtr buffer);
}
'@

$ptr = [IntPtr]::Zero
if (-not [CredApi]::CredRead('LegacyGeneric:target=git:https://x-access-token@github.com', 1, 0, [ref]$ptr)) {
    throw 'CredRead failed'
}
$blobSize = [Runtime.InteropServices.Marshal]::ReadInt32($ptr, 32)
$blobPtr = [Runtime.InteropServices.Marshal]::ReadIntPtr($ptr, 40)
$bytes = New-Object byte[] $blobSize
[Runtime.InteropServices.Marshal]::Copy($blobPtr, $bytes, 0, $blobSize)
[CredApi]::CredFree($ptr)
$token = [System.Text.Encoding]::Unicode.GetString($bytes).Trim()
if (-not $token.StartsWith('gho_')) { throw 'token format unexpected' }

$bodyText = (@(
  '## MikoMiko 0.1.51',
  '',
  '- 启动页下载游戏时不再在页面显示「正在写入：xxx」等文件级信息，只在鼠标悬浮下载按钮的悬浮卡中显示下载/安装双进度、剩余时间与实时速度',
  '- 修复视频右下角最大化后四周出现白边的问题，同时清理无边框全屏下的窗口 resize 边框与背景',
  '- 抽卡页去掉当前记录条数；游戏、UID、更新记录、记录设置、导出与记录目录收进右上角同一排，并重做游戏/UID 下拉框',
  '- 记录概览与出货分析改为覆盖在记录区上方的浮层，展开时不再把下方记录整体挤下去',
  '- 抽卡页禁止整页纵向滚动，只允许单个卡池内部的记录列表滚动',
  '- 每日任务验证码改为拿到极验结果后立即关闭弹窗，再由任务页提交校验，点击验证不再卡住；弹窗新增独立标题栏与关闭按钮',
  '- 设置页「通用设置」并入「应用维护」；「应用信息与关于」更名为「应用信息」，内容调整为关于在上、版本与数据目录在下',
  '',
  '安装包：`MikoMiko-Setup-0.1.51.exe`'
) -join "`n")

$body = @{
  tag_name = 'v0.1.51'
  name = 'MikoMiko 0.1.51'
  body = $bodyText
  draft = $false
  prerelease = $false
} | ConvertTo-Json -Depth 4 -Compress

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromMinutes(30)
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('token', $token)
$client.DefaultRequestHeaders.Accept.Add((New-Object System.Net.Http.Headers.MediaTypeWithQualityHeaderValue('application/vnd.github+json')))
$client.DefaultRequestHeaders.UserAgent.ParseAdd('MikoMiko-release')
$content = New-Object System.Net.Http.StringContent($body, [System.Text.Encoding]::UTF8, 'application/json')
$resp = $client.PostAsync('https://api.github.com/repos/Huiusto/MikoMiko/releases', $content).Result
$text = $resp.Content.ReadAsStringAsync().Result
if (-not $resp.IsSuccessStatusCode) { throw "release failed: $($resp.StatusCode) $text" }
$release = $text | ConvertFrom-Json
'release=' + $release.id
'url=' + $release.html_url

$file = (Resolve-Path 'dist\MikoMiko-Setup-0.1.51.exe').Path
$name = Split-Path $file -Leaf
$fs = [System.IO.File]::OpenRead($file)
$stream = New-Object System.Net.Http.StreamContent($fs)
$stream.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue('application/octet-stream')
$uploadUrl = "https://uploads.github.com/repos/Huiusto/MikoMiko/releases/$($release.id)/assets?name=$name"
$uploadResp = $client.PostAsync($uploadUrl, $stream).Result
$uploadText = $uploadResp.Content.ReadAsStringAsync().Result
$fs.Dispose()
if (-not $uploadResp.IsSuccessStatusCode) { throw "upload failed: $($uploadResp.StatusCode) $uploadText" }
$asset = $uploadText | ConvertFrom-Json
'asset=' + $asset.name
'state=' + $asset.state
'size=' + $asset.size
'url=' + $asset.browser_download_url
