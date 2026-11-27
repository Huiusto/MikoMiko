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
  '## MikoMiko 0.1.55',
  '',
  '- 每日任务验证器弹窗只显示极验面板本体：去掉外框关闭按钮、状态提示与加载白底，面板展开后窗口自动收紧到九宫格大小',
  '- 修复点击「完成验证」后米游社任务仍打卡失败：参考 TeyvatGuide 重做极验创建 / 校验，改用 `stoken/stuid/mid` 的 K2 签名与 `x-rpc-client_type: 2`，`getValidate()` 结果原样提交，不再拼接 `|jordan` 或改写字段；签到重试只携带 `x-rpc-challenge`',
  '',
  '安装包：`MikoMiko-Setup-0.1.55.exe`'
) -join "`n")

$body = @{
  tag_name = 'v0.1.55'
  name = 'MikoMiko 0.1.55'
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

$file = (Resolve-Path 'dist\MikoMiko-Setup-0.1.55.exe').Path
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
