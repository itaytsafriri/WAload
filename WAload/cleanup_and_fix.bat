@echo off
echo ========================================
echo WAload Cleanup and Fix Script
echo ========================================

set DOWNLOAD_FOLDER=C:\Users\Admin\Documents\WAload Downloads

echo Cleaning up processed files and fixing thumbnails folder...
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
echo Step 2: Removing thumbnail files from main folder
echo ========================================
for /f "delims=" %%f in ('dir /b "%DOWNLOAD_FOLDER%\thumb_*" 2^>nul') do (
    echo Deleting: %%f
    del "%DOWNLOAD_FOLDER%\%%f"
)

echo.
echo ========================================
echo Step 3: Creating and hiding thumbnails folder
echo ========================================
if exist "%DOWNLOAD_FOLDER%\.thumbnails" (
    echo Removing existing .thumbnails folder...
    rmdir /s /q "%DOWNLOAD_FOLDER%\.thumbnails"
)

echo Creating new hidden thumbnails folder...
mkdir "%DOWNLOAD_FOLDER%\.thumbnails"

echo Hiding the thumbnails folder...
attrib +h +s "%DOWNLOAD_FOLDER%\.thumbnails"

echo.
echo ========================================
echo Step 4: Verifying hidden folder
echo ========================================
attrib "%DOWNLOAD_FOLDER%\.thumbnails"
if %errorlevel% equ 0 (
    echo SUCCESS: Thumbnails folder is now hidden
) else (
    echo WARNING: Could not verify hidden status
)

echo.
echo ========================================
echo Cleanup and fix completed!
echo ========================================
echo.
echo The following changes were made:
echo 1. Removed all *_processed* files
echo 2. Removed all thumb_* files from main folder
echo 3. Recreated .thumbnails folder as hidden
echo 4. Applied hidden and system attributes
echo.
echo Your download folder should now be clean and properly configured.
echo The .thumbnails folder should be hidden from normal file explorer view.
pause 