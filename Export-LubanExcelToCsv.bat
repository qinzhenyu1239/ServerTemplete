@echo off
setlocal
set "SCRIPT_DIR=%~dp0tools\LubanExcelToCsv"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass ^
  -File "%SCRIPT_DIR%\Export-LubanExcelToCsv.ps1" ^
  -ConfigPath "%SCRIPT_DIR%\LubanExcelToCsv.json"

set "EXIT_CODE=%ERRORLEVEL%"
echo.
if not "%EXIT_CODE%"=="0" (
  echo [FAILED] Luban Excel to CSV export failed. Exit code: %EXIT_CODE%
) else (
  echo [SUCCESS] Luban Excel to CSV export completed.
)
echo.
pause
exit /b %EXIT_CODE%
