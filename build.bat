@echo off
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /codepage:65001 /optimize /win32icon:ZoneId-Cleaner.ico /win32manifest:ZoneId-Cleaner.manifest /out:ZoneId-Cleaner.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ZoneId-Cleaner.cs
if errorlevel 1 (echo Build failed) else (echo Build OK: ZoneId-Cleaner.exe)
