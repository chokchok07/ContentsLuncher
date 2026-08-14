@echo off
title Compiling Showroom Power Controller - Hardware Test Build
echo Compiling C# source code to PowerController.exe...
set ICON_FLAG=
if exist icon.ico (
    set ICON_FLAG=/win32icon:icon.ico
)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:PowerController.exe /r:System.Web.Extensions.dll %ICON_FLAG% PowerController.cs
if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] Compilation completed successfully. PowerController.exe generated!
) else (
    echo.
    echo [ERROR] Compilation failed. See compiler output errors above.
)
echo.
pause
