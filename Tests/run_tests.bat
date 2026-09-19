@echo off
setlocal
cd /d "%~dp0\.."

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
    echo [!] Error: csc.exe compiler not found.
    exit /b 1
)

echo [*] Compiling WinTime Test Suite...
"%CSC%" /nologo /target:exe /r:System.Net.Http.dll /optimize+ /platform:anycpu /out:Tests\UnitTests.exe Tests\UnitTests.cs Core\*.cs

if %errorlevel% neq 0 (
    echo [!] Failed to compile UnitTests.exe!
    exit /b 1
)

echo [*] Executing Unit Tests...
echo.
Tests\UnitTests.exe
set TEST_EXIT=%errorlevel%

:: Clean up test executable
if exist Tests\UnitTests.exe del /f /q Tests\UnitTests.exe

exit /b %TEST_EXIT%
