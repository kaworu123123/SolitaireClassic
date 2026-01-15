@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM === プロジェクト直下に置いて実行する想定 ===
set "ROOT=%~dp0"
set "BACKUP_DIR=%ROOT%backups"

REM タイムスタンプ（ロケールに依存しない）
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TS=%%i"

set "ZIP=%BACKUP_DIR%\UnityProject_%TS%.zip"

REM バックアップ先フォルダ作成
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

REM 必須フォルダの存在チェック
for %%D in (Assets ProjectSettings Packages) do (
  if not exist "%ROOT%%%D" (
    echo [ERROR] "%%D" フォルダが見つかりません。プロジェクト直下で実行してください。
    exit /b 1
  )
)

REM 7-Zip があれば優先利用（高速・堅牢）
where 7z >nul 2>&1
if %errorlevel%==0 (
  echo Using 7-Zip...
  pushd "%ROOT%"
  7z a -tzip -mx=7 "%ZIP%" ".\Assets\*" ".\ProjectSettings\*" ".\Packages\*"
  popd
) else (
  REM 7-Zip が無ければ PowerShell の Compress-Archive を使用
  echo Using PowerShell Compress-Archive...
  powershell -NoProfile -Command ^
    "Compress-Archive -Path @('Assets','ProjectSettings','Packages') -DestinationPath '%ZIP%' -Force"
)

if exist "%ZIP%" (
  echo.
  echo ✅ Backup completed: "%ZIP%"
)
