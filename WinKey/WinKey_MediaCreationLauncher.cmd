@echo off
setlocal EnableExtensions

set "SOURCE=%~dp0MediaCreationTool.bat"
set "PATCHED=%TEMP%\WinKey-MediaCreationTool-%RANDOM%%RANDOM%.bat"

if not exist "%SOURCE%" (
    echo ERROR: MediaCreationTool.bat was not found next to WinKey.exe.
    exit /b 1
)

copy /y "%SOURCE%" "%PATCHED%" >nul
if errorlevel 1 (
    echo ERROR: Could not prepare the Media Creation Tool.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:PATCHED; $s=[IO.File]::ReadAllText($p); $old=':choice-14`r`nset ""VER=19045"" & set ""VID=22H2"" & set ""CB=19045.2965.230505-1139.22h2_release_svc_refresh"" & set ""CT=2023/05/"" & set ""CC=1.4.1""`r`nset ""CAB=https://download.microsoft.com/download/3/c/9/3c959fca-d288-46aa-b578-2a6c6c33137a/products_win10_20230510.cab""`r`nset ""EXE=https://download.microsoft.com/download/9/e/a/9eac306f-d134-4609-9c58-35d1638c2363/MediaCreationTool22H2.exe""'; $new=':choice-14`r`nset ""VER=19045"" & set ""VID=22H2"" & set ""CB=19045.3803.231204-0204.22h2_release_svc_refresh"" & set ""CT=2023/12/"" & set ""CC=1.4.1""`r`nset ""CAB=https://download.microsoft.com/download/7/9/c/79cbc22a-0eea-4a0d-89c0-054a1b3aa8e0/products.cab""`r`nset ""EXE=https://download.microsoft.com/download/9/e/a/9eac306f-d134-4609-9c58-35d1638c2363/MediaCreationTool_22H2.exe""'; if($s.Contains($old)){[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.Encoding]::ASCII); exit 0}else{Write-Host 'WARNING: The expected Windows 10 22H2 block was not found. Running the bundled script unchanged.'; exit 0}"

call "%PATCHED%" %*
set "EXITCODE=%ERRORLEVEL%"

del /f /q "%PATCHED%" >nul 2>nul
exit /b %EXITCODE%
