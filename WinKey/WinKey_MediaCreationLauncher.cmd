@echo off
setlocal EnableExtensions

set "SOURCE=%~dp0MediaCreationTool.bat"
set "BUNDLED22H2=%~dp022H2"
set "WORK=%SystemDrive%\ESD\MCT"
set "PATCHED=%TEMP%\WinKey-MediaCreationTool-%RANDOM%%RANDOM%.bat"

if not exist "%SOURCE%" (
    echo ERROR: MediaCreationTool.bat was not found next to WinKey.exe.
    echo.
    pause
    exit /b 1
)

if not exist "%BUNDLED22H2%\MediaCreationTool_22H2.exe" (
    echo ERROR: Bundled Windows 10 22H2 Media Creation Tool was not found:
    echo %BUNDLED22H2%\MediaCreationTool_22H2.exe
    echo.
    pause
    exit /b 1
)

if not exist "%BUNDLED22H2%\products.cab" (
    echo ERROR: Bundled Windows 10 22H2 products.cab was not found:
    echo %BUNDLED22H2%\products.cab
    echo.
    pause
    exit /b 1
)

mkdir "%WORK%" >nul 2>nul
copy /y "%BUNDLED22H2%\MediaCreationTool_22H2.exe" "%WORK%\MediaCreationTool22H2.exe" >nul
if errorlevel 1 (
    echo ERROR: Could not prepare the bundled Windows 10 22H2 Media Creation Tool.
    echo.
    pause
    exit /b 1
)

copy /y "%BUNDLED22H2%\products.cab" "%WORK%\products22H2.cab" >nul
if errorlevel 1 (
    echo ERROR: Could not prepare the bundled Windows 10 22H2 products catalog.
    echo.
    pause
    exit /b 1
)

echo Bundled Windows 10 22H2 files prepared. Selecting 22H2 will use these local files instead of downloading them.

copy /y "%SOURCE%" "%PATCHED%" >nul
if errorlevel 1 (
    echo ERROR: Could not prepare the Media Creation Tool.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:PATCHED; try { $s=[IO.File]::ReadAllText($p); $nl=if($s.Contains([Environment]::NewLine)){[Environment]::NewLine}else{[string][char]10}; $pattern='(?ms)^:choice-14\r?\n.*?(?=^:choice-13\b)'; if(-not [regex]::IsMatch($s,$pattern)){Write-Host 'ERROR: Windows 10 22H2 section was not found. The Media Creation Tool was not started.'; exit 2}; $lines=@(':choice-14','set \"VER=19045\" & set \"VID=22H2\" & set \"CB=19045.3803.231204-0204.22h2_release_svc_refresh\" & set \"CT=2023/12/\" & set \"CC=1.4.1\"','set \"CAB=https://download.microsoft.com/download/7/9/c/79cbc22a-0eea-4a0d-89c0-054a1b3aa8e0/products.cab\"','set \"EXE=https://download.microsoft.com/download/9/e/a/9eac306f-d134-4609-9c58-35d1638c2363/MediaCreationTool_22H2.exe\"','goto process ::# refreshed 19041 base with integrated 22H2 enablement package - current',''); $new=$lines -join $nl; $updated=[regex]::Replace($s,$pattern,$new,1); [IO.File]::WriteAllText($p,$updated,[Text.UTF8Encoding]::new($false)); $verify=[IO.File]::ReadAllText($p); if($verify -notmatch '19045\.3803\.231204-0204\.22h2_release_svc_refresh'){Write-Host 'ERROR: Windows 10 22H2 update verification failed.'; exit 3}; Write-Host 'Windows 10 22H2 settings updated and verified for this run.'; exit 0 } catch { Write-Host ('ERROR: Failed to update Windows 10 22H2 settings: ' + $_.Exception.Message); exit 1 }"
set "PATCH_RESULT=%ERRORLEVEL%"

if not "%PATCH_RESULT%"=="0" (
    echo.
    echo MediaCreationTool.bat was not started because the Windows 10 22H2 update could not be verified.
    del /f /q "%PATCHED%" >nul 2>nul
    echo.
    pause
    exit /b %PATCH_RESULT%
)

echo.
echo Starting MediaCreationTool.bat...
echo.
call "%PATCHED%" %*
set "EXITCODE=%ERRORLEVEL%"

del /f /q "%PATCHED%" >nul 2>nul

echo.
echo ================================================================
if "%EXITCODE%"=="0" (
    echo MediaCreationTool.bat finished successfully.
) else (
    echo MediaCreationTool.bat exited with code %EXITCODE%.
    echo Review the messages above for the actual failure.
)
echo ================================================================
echo.
pause
exit /b %EXITCODE%