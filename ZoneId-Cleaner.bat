@echo off
setlocal
echo ZoneID 一括解除ツール
echo.

if "%~1"=="" (
    echo 【使い方】
    echo Zone.Identifier を解除したいファイルまたはフォルダーを、このバッチファイルにドロップしてください。
    echo 複数のファイルやフォルダーをまとめてドロップすることも可能です。
    echo.
    pause
    exit /b
)

rem shift すると %0 もずれるため、スクリプトの場所を先に保存する
set "SCRIPT_DIR=%~dp0"
set "EXITCODE=0"

rem 引数を 1 つずつ引用符付きで渡す。%* をそのまま渡すと、記号を含むパスで引数が崩れることがある
:loop
if "%~1"=="" goto done
set "ARG=%~1"
rem 末尾が \ のパス（D:\ など）は、そのままだと \" がエスケープと解釈されるため \ を重ねる
if "%ARG:~-1%"=="\" set "ARG=%ARG%\"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%ZoneId-Cleaner.ps1" "%ARG%"
if errorlevel 1 set "EXITCODE=1"
shift
goto loop

:done
echo.
if "%EXITCODE%"=="0" (
    echo すべての処理が完了しました。
) else (
    echo 一部の処理に失敗しました。上のログを確認してください。
)
pause
exit /b %EXITCODE%
