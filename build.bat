@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
title Build WinTime (Native C#)
echo.
echo  ==============================================================
echo               BUILDING WINTIME (NATIVE C#)
echo  ==============================================================
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
    echo  [!] Error: Built-in Microsoft C# compiler csc.exe was not found.
    pause
    exit /b 1
)

echo  [*] Compiling Program.cs with native Windows C# compiler and icon...
"%CSC%" /nologo /target:winexe /r:System.Net.Http.dll /warn:4 /warnaserror /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:app.ico /out:WinTime.exe Program.cs Core\*.cs

if not errorlevel 1 (
    echo.
    echo  [OK] Successfully compiled WinTime.exe!
    for %%I in (WinTime.exe) do echo  [OK] Binary size: %%~zI bytes

    :: Optional Code Signing with signtool.exe if certificate.pfx is present
    if exist "%~dp0certificate.pfx" (
        where signtool.exe >nul 2>&1
        if not errorlevel 1 (
            echo  [*] Signing WinTime.exe with code signing certificate...
            signtool.exe sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "%~dp0certificate.pfx" WinTime.exe >nul 2>&1
            if not errorlevel 1 (
                echo  [OK] Successfully digitally signed WinTime.exe!
            ) else (
                echo  [!] Warning: Code signing failed.
            )
        )
    )
    echo.
) else (
    echo.
    echo  [!] Failed to compile WinTime.exe.
    echo  [!] If WinTime.exe is currently running, close it first and retry.
    echo.
)

pause
