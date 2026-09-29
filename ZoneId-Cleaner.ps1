Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class ZoneUtil {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool DeleteFile(string p);
}
'@ -ErrorAction SilentlyContinue

function Remove-ZoneIdentifier {
    param([string]$FilePath)
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
        } else {
            Write-Host ('  [解除失敗] ' + $FilePath) -ForegroundColor Red
        }
    } else {
        Write-Host ('  [解除不要] ' + $FilePath) -ForegroundColor DarkGray
    }
}

# 引数（$args）で受け取ったすべてのファイル・フォルダーを処理
foreach ($target in $args) {
    if (Test-Path -LiteralPath ($target + '\') -PathType Container) {
        Write-Host ("[FOLDER] $target 内のファイルを処理中...")
        [System.IO.Directory]::EnumerateFiles($target, '*', [System.IO.SearchOption]::AllDirectories) | ForEach-Object {
            Remove-ZoneIdentifier -FilePath $_
        }
    } else {
        Write-Host ("[FILE] $target")
        Remove-ZoneIdentifier -FilePath $target
    }
}