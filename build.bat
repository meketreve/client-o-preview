@echo off
echo ===================================
echo  Client-O-Preview Build Script
echo ===================================
echo.

cd /d "%~dp0"

echo Cleaning previous build...
dotnet clean -c Release

echo.
echo Building release...
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false

echo.
if %ERRORLEVEL% EQU 0 (
    echo ===================================
    echo  Build completed successfully!
    echo  Output: bin\Release\net8.0-windows\win-x64\publish\ClientOPreview.exe
    echo ===================================
) else (
    echo ===================================
    echo  Build failed with error code %ERRORLEVEL%
    echo ===================================
)

echo.
pause
