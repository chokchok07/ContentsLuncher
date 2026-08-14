@echo off
echo ==============================================
echo [BUILD] Compiling PowerController.cs (WinForms)...
echo ==============================================

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC_PATH%" (
    echo [ERROR] csc.exe compiler not found in 64-bit or 32-bit Framework path.
    echo Please ensure .NET Framework v4.0 or higher is installed.
    pause
    exit /b 1
)

echo [RUN] "%CSC_PATH%" /target:winexe /out:PowerController.exe /r:System.Web.Extensions.dll PowerController.cs
"%CSC_PATH%" /target:winexe /out:PowerController.exe /r:System.Web.Extensions.dll PowerController.cs

if %errorlevel% neq 0 (
    echo [ERROR] Compilation failed.
    pause
    exit /b %errorlevel%
)

echo.
echo ==============================================
echo [SUCCESS] PowerController.exe built successfully!
echo ==============================================
echo.
echo You can run the controller by double-clicking PowerController.exe.
echo.
pause
