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
call :Download "%RELEASES_LATEST%/WinTime.exe" "%INSTALL_DIR%\WinTime.exe"
if exist "%INSTALL_DIR%\WinTime.exe" (
    echo       [OK] WinTime.exe downloaded.
) else (
    echo       [-] WinTime.exe not found in latest release, downloading script engine...
)

:: 2. Download WinTime.bat launcher
echo       Downloading WinTime.bat ...
call :Download "%REPO_RAW%/WinTime.bat" "%INSTALL_DIR%\WinTime.bat"

:: 3. Download WinTime.ps1 CLI engine
echo       Downloading WinTime.ps1 ...
call :Download "%REPO_RAW%/WinTime.ps1" "%INSTALL_DIR%\WinTime.ps1"

:: 4. Download app.ico
echo       Downloading app.ico ...
call :Download "%REPO_RAW%/app.ico" "%INSTALL_DIR%\app.ico"

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

:: 5. Create Desktop and Start Menu shortcuts for BOTH GUI and CLI
echo [*] Creating Desktop and Start Menu shortcuts...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$dPath = [Environment]::GetFolderPath('Desktop');" ^
  "$smDir = [Environment]::GetFolderPath('Programs') + '\WinTime';" ^
  "if (-not (Test-Path $smDir)) { New-Item -ItemType Directory -Path $smDir -Force | Out-Null };" ^
  "if (Test-Path '%INSTALL_DIR%\WinTime.exe') {" ^
  "  $gDesk = $ws.CreateShortcut($dPath + '\WinTime (GUI).lnk');" ^
  "  $gDesk.TargetPath = '%INSTALL_DIR%\WinTime.exe';" ^
  "  $gDesk.WorkingDirectory = '%INSTALL_DIR%';" ^
  "  if (Test-Path '%INSTALL_DIR%\app.ico') { $gDesk.IconLocation = '%INSTALL_DIR%\app.ico, 0' };" ^
  "  $gDesk.Description = 'WinTime - Graphical Time Manager';" ^
  "  $gDesk.Save();" ^
  "  $gMenu = $ws.CreateShortcut($smDir + '\WinTime (GUI).lnk');" ^
  "  $gMenu.TargetPath = '%INSTALL_DIR%\WinTime.exe';" ^
  "  $gMenu.WorkingDirectory = '%INSTALL_DIR%';" ^
  "  if (Test-Path '%INSTALL_DIR%\app.ico') { $gMenu.IconLocation = '%INSTALL_DIR%\app.ico, 0' };" ^
  "  $gMenu.Description = 'WinTime - Graphical Time Manager';" ^
  "  $gMenu.Save();" ^
  "};" ^
  "if (Test-Path '%INSTALL_DIR%\WinTime.bat') {" ^
  "  $cDesk = $ws.CreateShortcut($dPath + '\WinTime (CLI).lnk');" ^
  "  $cDesk.TargetPath = '%INSTALL_DIR%\WinTime.bat';" ^
  "  $cDesk.WorkingDirectory = '%INSTALL_DIR%';" ^
  "  if (Test-Path '%INSTALL_DIR%\app.ico') { $cDesk.IconLocation = '%INSTALL_DIR%\app.ico, 0' };" ^
  "  $cDesk.Description = 'WinTime - Command Line Interface';" ^
  "  $cDesk.Save();" ^
  "  $cMenu = $ws.CreateShortcut($smDir + '\WinTime (CLI).lnk');" ^
  "  $cMenu.TargetPath = '%INSTALL_DIR%\WinTime.bat';" ^
  "  $cMenu.WorkingDirectory = '%INSTALL_DIR%';" ^
  "  if (Test-Path '%INSTALL_DIR%\app.ico') { $cMenu.IconLocation = '%INSTALL_DIR%\app.ico, 0' };" ^
  "  $cMenu.Description = 'WinTime - Command Line Interface';" ^
  "  $cMenu.Save();" ^
  "}"

:: 6. Create uninstaller
set "UNINSTALL_BAT=%INSTALL_DIR%\uninstall.bat"
(
echo @echo off
echo echo Uninstalling WinTime...
echo del /f /q "%%USERPROFILE%%\Desktop\WinTime*.lnk" 2^>nul
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
echo  [OK] Shortcuts created for GUI and CLI!
echo ================================================================
echo.

if /i "%~1"=="-cli" goto LaunchCli
if /i "%~1"=="--cli" goto LaunchCli
if /i "%~1"=="/cli" goto LaunchCli

if exist "%INSTALL_DIR%\WinTime.exe" (
    echo [*] Launching WinTime (GUI)...
    start "" "%INSTALL_DIR%\WinTime.exe"
    exit /b 0
)

:LaunchCli
echo [*] Launching WinTime (CLI)...
call "%INSTALL_DIR%\WinTime.bat"
exit /b 0

:: Universal download subroutine: uses curl if available, falls back to native WebClient
:Download
set "DL_URL=%~1"
set "DL_DEST=%~2"
where curl >nul 2>&1
if %ERRORLEVEL% equ 0 (
    curl.exe -sSfL -m 30 "%DL_URL%" -o "%DL_DEST%" >nul 2>&1
    if exist "%DL_DEST%" exit /b 0
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "[System.Net.ServicePointManager]::SecurityProtocol = 3072; (New-Object System.Net.WebClient).DownloadFile('%DL_URL%', '%DL_DEST%')" >nul 2>&1
if exist "%DL_DEST%" exit /b 0
exit /b 1
