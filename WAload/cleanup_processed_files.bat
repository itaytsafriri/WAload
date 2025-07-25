@echo off
echo ========================================
echo WAload Cleanup Script
echo ========================================

set DOWNLOAD_FOLDER=C:\Users\Admin\Documents\WAload Downloads

echo Cleaning up processed files and thumbnails...
echo Download folder: %DOWNLOAD_FOLDER%

if not exist "%DOWNLOAD_FOLDER%" (
    echo ERROR: Download folder not found: %DOWNLOAD_FOLDER%
    echo Please update the DOWNLOAD_FOLDER variable in this script
    pause
    exit /b 1
)

echo.
echo ========================================
echo Step 1: Removing processed files
echo ========================================
for /f "delims=" %%f in ('dir /b "%DOWNLOAD_FOLDER%\*_processed*" 2^>nul') do (
    echo Deleting: %%f
    del "%DOWNLOAD_FOLDER%\%%f"
)

echo.
echo ========================================
echo Step 2: Removing thumbnail files
echo ========================================
for /f "delims=" %%f in ('dir /b "%DOWNLOAD_FOLDER%\thumb_*" 2^>nul') do (
    echo Deleting: %%f
    del "%DOWNLOAD_FOLDER%\%%f"
)

echo.
echo ========================================
echo Step 3: Creating hidden thumbnails folder
echo ========================================
if not exist "%DOWNLOAD_FOLDER%\.thumbnails" (
    mkdir "%DOWNLOAD_FOLDER%\.thumbnails"
    echo Created hidden thumbnails folder
) else (
    echo Hidden thumbnails folder already exists
)

echo.
echo ========================================
echo Cleanup completed!
echo ========================================
echo.
echo The following changes were made:
echo 1. Removed all *_processed* files
echo 2. Removed all thumb_* files from main folder
echo 3. Created hidden .thumbnails folder
echo.
echo Your download folder should now be clean.
pause 