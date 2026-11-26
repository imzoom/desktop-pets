@echo off
setlocal
title Desktop Pet Launcher

echo ========================================================
echo        Desktop Pet - Character Studio Launcher
echo ========================================================
echo.

:: 1. Check if standalone release executable exists
if exist "%~dp0release\DesktopPet-Standalone-win-x64\DesktopPet.exe" (
    echo [INFO] Launching standalone zero-dependency Desktop Pet...
    start "" "%~dp0release\DesktopPet-Standalone-win-x64\DesktopPet.exe"
    exit /b 0
)

:: 2. Check if dotnet is installed to run source
where dotnet >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo [INFO] Running Desktop Pet via .NET...
    cd /d "%~dp0desktop-pet-win"
    dotnet run
    exit /b 0
)

:: 3. Neither found - guide the user
echo [ERROR] No standalone executable or .NET SDK found!
echo.
echo To run Desktop Pet:
echo   1. If you want to run without installing .NET:
echo      Download 'DesktopPet-Standalone-Win64.zip' from GitHub Releases
echo      and double-click DesktopPet.exe.
echo.
echo   2. Or install .NET SDK from:
echo      https://dotnet.microsoft.com/download
echo.
pause
