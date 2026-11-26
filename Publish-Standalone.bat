@echo off
setlocal
title Desktop Pet - Standalone Builder

echo ========================================================
echo      Building Standalone Zero-Dependency Executable
echo ========================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK is required on this build machine to publish.
    echo Please install .NET from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [1/3] Publishing self-contained win-x64 single-file executable...
cd /d "%~dp0desktop-pet-win"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "..\release\DesktopPet-Standalone-win-x64"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [FAILED] Publish failed with error code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/3] Creating distribution ZIP package...
powershell -NoProfile -Command "Compress-Archive -Path '%~dp0release\DesktopPet-Standalone-win-x64\*' -DestinationPath '%~dp0release\DesktopPet-Standalone-Win64.zip' -Force"

echo.
echo [3/3] Success! Standalone distribution ready at:
echo   Executable: release\DesktopPet-Standalone-win-x64\DesktopPet.exe
echo   Zip Package: release\DesktopPet-Standalone-Win64.zip
echo.
echo You can send DesktopPet.exe or DesktopPet-Standalone-Win64.zip
echo to ANYONE on Windows - it runs directly with NO .NET required!
echo.
pause
