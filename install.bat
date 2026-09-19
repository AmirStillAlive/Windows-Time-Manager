@echo off
setlocal EnableDelayedExpansion
title WinTime Automated Installer

echo ================================================================
echo                   WINTIME - AUTOMATED INSTALLER
echo ================================================================
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\WinTime"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

set "REPO_RAW=https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main"
set "RELEASES_LATEST=https://github.com/AmirStillAlive/Windows-Time-Manager/releases/latest/download"

echo [*] Downloading WinTime components...

:: 1. Try downloading WinTime.exe from latest release
echo       Downloading WinTime.exe ...
curl -sSfL -m 30 "%RELEASES_LATEST%/WinTime.exe" -o "%INSTALL_DIR%\WinTime.exe" >nul 2>&1
if exist "%INSTALL_DIR%\WinTime.exe" (
    echo       [OK] WinTime.exe downloaded.
) else (
    echo       [-] WinTime.exe not found in latest release assets, downloading script engine...
)

:: 2. Download WinTime.bat launcher
echo       Downloading WinTime.bat ...
curl -sSfL -m 15 "%REPO_RAW%/WinTime.bat" -o "%INSTALL_DIR%\WinTime.bat" >nul 2>&1

:: 3. Download WinTime.ps1 CLI engine
echo       Downloading WinTime.ps1 ...
curl -sSfL -m 15 "%REPO_RAW%/WinTime.ps1" -o "%INSTALL_DIR%\WinTime.ps1" >nul 2>&1

:: 4. Download app.ico
echo       Downloading app.ico ...
curl -sSfL -m 15 "%REPO_RAW%/app.ico" -o "%INSTALL_DIR%\app.ico" >nul 2>&1

:: Check if at least WinTime.exe OR (WinTime.ps1 + WinTime.bat) exists
if not exist "%INSTALL_DIR%\WinTime.exe" (
    if not exist "%INSTALL_DIR%\WinTime.ps1" (
        echo.
        echo ================================================================
        echo  [X] CRITICAL INSTALLATION FAILURE
        echo  Could not download components. Check your internet connection.
        echo ================================================================
        pause
        exit /b 1
    )
)

:: 5. Create Desktop and Start Menu shortcuts
echo [*] Creating shortcuts...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$isExe = Test-Path '%INSTALL_DIR%\WinTime.exe';" ^
  "$target = if ($isExe) { '%INSTALL_DIR%\WinTime.exe' } else { '%INSTALL_DIR%\WinTime.bat' };" ^
  "$dShortcut = $ws.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\WinTime.lnk');" ^
  "$dShortcut.TargetPath = $target;" ^
  "$dShortcut.WorkingDirectory = '%INSTALL_DIR%';" ^
  "if (Test-Path '%INSTALL_DIR%\app.ico') { $dShortcut.IconLocation = '%INSTALL_DIR%\app.ico, 0' };" ^
  "$dShortcut.Description = 'WinTime - Windows Time & NTP Manager';" ^
  "$dShortcut.Save();" ^
  "$smDir = [Environment]::GetFolderPath('Programs') + '\WinTime';" ^
  "if (-not (Test-Path $smDir)) { New-Item -ItemType Directory -Path $smDir -Force | Out-Null };" ^
  "$sShortcut = $ws.CreateShortcut($smDir + '\WinTime.lnk');" ^
  "$sShortcut.TargetPath = $target;" ^
  "$sShortcut.WorkingDirectory = '%INSTALL_DIR%';" ^
  "if (Test-Path '%INSTALL_DIR%\app.ico') { $sShortcut.IconLocation = '%INSTALL_DIR%\app.ico, 0' };" ^
  "$sShortcut.Description = 'WinTime - Windows Time & NTP Manager';" ^
  "$sShortcut.Save();"

:: 6. Create uninstaller
set "UNINSTALL_BAT=%INSTALL_DIR%\uninstall.bat"
(
echo @echo off
echo echo Uninstalling WinTime...
echo del /f /q "%%USERPROFILE%%\Desktop\WinTime.lnk" 2^>nul
echo rd /s /q "%%APPDATA%%\Microsoft\Windows\Start Menu\Programs\WinTime" 2^>nul
echo timeout /t 1 /nobreak ^>nul
echo rd /s /q "%INSTALL_DIR%" 2^>nul
echo echo [OK] WinTime has been successfully uninstalled.
echo timeout /t 2 /nobreak ^>nul
) > "%UNINSTALL_BAT%"

:: 7. Unblock downloaded files (clear MOTW)
echo [*] Clearing web flags (MOTW)...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%INSTALL_DIR%' | Unblock-File -ErrorAction SilentlyContinue"

echo.
echo ================================================================
echo  [OK] Successfully installed WinTime to:
echo       %INSTALL_DIR%
echo  [OK] Desktop and Start Menu shortcuts created!
echo ================================================================
echo.
echo [*] Launching WinTime...
if exist "%INSTALL_DIR%\WinTime.exe" (
    start "" "%INSTALL_DIR%\WinTime.exe"
) else (
    start "" "%INSTALL_DIR%\WinTime.bat"
)
exit /b 0
