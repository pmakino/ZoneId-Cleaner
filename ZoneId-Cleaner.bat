@echo off
setlocal
echo ZoneID 一括削除ツール
echo.

if "%~1"=="" (
    echo 【使い方】
    echo Zone.Identifier を解除したいファイルまたはフォルダーを、このバッチファイルにドロップしてください。
    echo 複数のファイルやフォルダーをまとめてドロップすることも可能です。
    echo.
    pause
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ZoneId-Cleaner.ps1" %*

echo.
echo すべての処理が完了しました。
pause