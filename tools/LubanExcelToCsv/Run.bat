@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Export-LubanExcelToCsv.ps1" %*
if errorlevel 1 (
    echo.
    echo Export failed.
    pause
    exit /b 1
)
echo.
echo Export completed.
pause
