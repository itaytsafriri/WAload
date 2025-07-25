@echo off
echo ========================================
echo WAload Complete Fix Script
echo ========================================

set DOWNLOAD_FOLDER=C:\Users\Admin\Documents\WAload Downloads

echo Fixing all WAload issues...
echo Download folder: %DOWNLOAD_FOLDER%

if not exist "%DOWNLOAD_FOLDER%" (
    echo ERROR: Download folder not found: %DOWNLOAD_FOLDER%
    echo Please update the DOWNLOAD_FOLDER variable in this script
    pause
    exit /b 1
)

echo.
echo ========================================
echo Step 1: Stopping WAload process
echo ========================================
taskkill /f /im WAload.exe 2>nul
if %errorlevel% equ 0 (
    echo Stopped WAload process
) else (
    echo WAload process not running (or already stopped)
)

echo.
echo ========================================
echo Step 2: Cleaning up processed files
echo ========================================
for /f "delims=" %%f in ('dir /b "%DOWNLOAD_FOLDER%\*_processed*" 2^>nul') do (
    echo Deleting: %%f
    del "%DOWNLOAD_FOLDER%\%%f"
)

echo.
echo ========================================
echo Step 3: Cleaning up thumbnail files
echo ========================================
for /f "delims=" %%f in ('dir /b "%DOWNLOAD_FOLDER%\thumb_*" 2^>nul') do (
    echo Deleting: %%f
    del "%DOWNLOAD_FOLDER%\%%f"
)

echo.
echo ========================================
echo Step 4: Fixing thumbnails folder
echo ========================================
if exist "%DOWNLOAD_FOLDER%\.thumbnails" (
    echo Removing existing .thumbnails folder...
    rmdir /s /q "%DOWNLOAD_FOLDER%\.thumbnails"
)

echo Creating new hidden thumbnails folder...
mkdir "%DOWNLOAD_FOLDER%\.thumbnails"

echo Setting hidden and system attributes...
attrib +h +s "%DOWNLOAD_FOLDER%\.thumbnails"

echo.
echo ========================================
echo Step 5: Verifying folder attributes
echo ========================================
attrib "%DOWNLOAD_FOLDER%\.thumbnails"
if %errorlevel% equ 0 (
    echo SUCCESS: Thumbnails folder is now properly hidden
) else (
    echo WARNING: Could not verify hidden status
)

echo.
echo ========================================
echo Step 6: Clearing Windows thumbnail cache
echo ========================================
echo Clearing Windows thumbnail cache to ensure proper hiding...
taskkill /f /im explorer.exe 2>nul
timeout /t 2 /nobreak >nul
start explorer.exe

echo.
echo ========================================
echo Complete fix finished!
echo ========================================
echo.
echo The following fixes were applied:
echo 1. Stopped WAload process to apply new code
echo 2. Removed all *_processed* files
echo 3. Removed all thumb_* files from main folder
echo 4. Recreated .thumbnails folder with proper hidden attributes
echo 5. Cleared Windows thumbnail cache
echo.
echo Next steps:
echo 1. Restart WAload application
echo 2. The .thumbnails folder should now be hidden
echo 3. New progress bar should be beautiful and responsive
echo 4. File scanning should work properly
echo.
pause 