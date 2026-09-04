@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-win-x64.ps1"
set "PUBLISH_EXIT_CODE=%ERRORLEVEL%"

if not "%PUBLISH_EXIT_CODE%"=="0" (
    echo.
    echo Publishing failed. See the error above.
    pause
    exit /b %PUBLISH_EXIT_CODE%
)

echo.
echo Publishing completed. Automatic application launch attempted; see the result above.
pause
