Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class ZoneUtil {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool DeleteFile(string p);
}
'@ -ErrorAction SilentlyContinue

$script:Count = @{ Success = 0; Failure = 0; Skipped = 0 }

function Remove-ZoneIdentifier {
    param([string]$FilePath)
    # 相対パスを絶対パスに正規化（\?\ 付与には絶対パスが必要）
    try {
        $FilePath = [System.IO.Path]::GetFullPath($FilePath)
    } catch {
        Write-Host ('  [解除失敗] ' + $FilePath + ' (' + $_.Exception.Message + ')') -ForegroundColor Red
        $script:Count.Failure++
        return
    }
    $hasZone = Get-Item -LiteralPath $FilePath -Stream Zone.Identifier -ErrorAction SilentlyContinue
    if ($hasZone) {
        if ($FilePath.StartsWith('\\?\')) {
            $prefixed = $FilePath
        } elseif ($FilePath.StartsWith('\\')) {
            # UNC パス: \\server\share -> \\?\UNC\server\share
            $prefixed = '\\?\UNC\' + $FilePath.Substring(2)
        } else {
            $prefixed = '\\?\' + $FilePath
        }
        $lp = $prefixed + ':Zone.Identifier'
        $ok = [ZoneUtil]::DeleteFile($lp)
        if ($ok) {
            Write-Host ('  [解除成功] ' + $FilePath) -ForegroundColor Green
            $script:Count.Success++
        } else {
            $code = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
            $msg = (New-Object System.ComponentModel.Win32Exception($code)).Message
            Write-Host ('  [解除失敗] ' + $FilePath + " (エラー $code`: $msg)") -ForegroundColor Red
            $script:Count.Failure++
        }
    } else {
        Write-Host ('  [解除不要] ' + $FilePath) -ForegroundColor DarkGray
        $script:Count.Skipped++
    }
}

# フォルダーを再帰的に処理する。アクセス拒否のフォルダーは失敗として記録し、残りの処理は続行する
function Invoke-Folder {
    param([string]$Folder)
    try {
        $files = [System.IO.Directory]::GetFiles($Folder)
        $dirs = [System.IO.Directory]::GetDirectories($Folder)
    } catch {
        Write-Host ('  [列挙失敗] ' + $Folder + ' (' + $_.Exception.Message + ')') -ForegroundColor Red
        $script:Count.Failure++
        return
    }
    foreach ($f in $files) { Remove-ZoneIdentifier -FilePath $f }
    foreach ($d in $dirs) { Invoke-Folder -Folder $d }
}

# 引数（$args）で受け取ったすべてのファイル・フォルダーを処理
foreach ($target in $args) {
    if (Test-Path -LiteralPath ($target + '\') -PathType Container) {
        Write-Host ("[FOLDER] $target 内のファイルを処理中...")
        Invoke-Folder -Folder ([System.IO.Path]::GetFullPath($target))
    } else {
        Write-Host ("[FILE] $target")
        Remove-ZoneIdentifier -FilePath $target
    }
}

Write-Host ''
Write-Host ('結果: 成功 {0} / 失敗 {1} / 解除不要 {2}' -f $script:Count.Success, $script:Count.Failure, $script:Count.Skipped)
if ($script:Count.Failure -gt 0) { exit 1 }
