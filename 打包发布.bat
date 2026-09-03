@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-win-x64.ps1"
set "PACKAGE_EXIT_CODE=%ERRORLEVEL%"

if not "%PACKAGE_EXIT_CODE%"=="0" (
    echo.
    echo Packaging failed. See the error above.
    pause
    exit /b %PACKAGE_EXIT_CODE%
)

echo.
echo Packaging completed. Extract artifacts\PoeTradeAssistant-win-x64.zip completely before starting PoeTradeAssistant.exe.
pause
