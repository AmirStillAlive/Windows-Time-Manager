@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

if not exist "%~dp0WinTime.ps1" (
    echo [!] Error: WinTime.ps1 was not found in the expected location:
    echo     "%~dp0WinTime.ps1"
    echo.
    echo Please make sure WinTime.bat and WinTime.ps1 are in the same folder.
    pause
    exit /b 1
)

:: Best-effort clear Mark-of-the-Web (Zone.Identifier) before launching
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%~dp0WinTime.ps1' -ErrorAction SilentlyContinue" >nul 2>&1

:: Locate PowerShell executable (standard Windows PowerShell or PowerShell 7+)
set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "!PS_EXE!" (
    where pwsh.exe >nul 2>&1
    if not errorlevel 1 (
        set "PS_EXE=pwsh.exe"
    ) else (
        where powershell.exe >nul 2>&1
        if not errorlevel 1 (
            set "PS_EXE=powershell.exe"
        ) else (
            echo [!] Error: Neither Windows PowerShell nor pwsh.exe could be found.
            pause
            exit /b 1
        )
    )
)

"!PS_EXE!" -NoProfile -ExecutionPolicy Bypass -File "%~dp0WinTime.ps1" %*
set "EXIT_CODE=!ERRORLEVEL!"

if !EXIT_CODE! neq 0 (
    echo.
    echo [!] WinTime exited with error code: !EXIT_CODE!
    pause
)

exit /b !EXIT_CODE!
