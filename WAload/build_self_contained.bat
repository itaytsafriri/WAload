@echo off
echo ========================================
echo WAload Self-Contained Build Script
echo ========================================
echo.

REM Clean previous builds
echo [1/4] Cleaning previous builds...
dotnet clean WAload\WAload.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to clean project
    pause
    exit /b 1
)

REM Restore packages
echo [2/4] Restoring NuGet packages...
dotnet restore WAload\WAload.csproj
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)

REM Build self-contained application
echo [3/4] Building self-contained application...
dotnet publish WAload\WAload.csproj --configuration Release --runtime win-x64 --self-contained true --output "WAload\bin\Release\net9.0-windows\win-x64\publish"
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to build self-contained application
    pause
    exit /b 1
)

echo [4/4] Build completed successfully!
echo.

echo ========================================
echo BUILD SUMMARY
echo ========================================
echo Output Directory: WAload\bin\Release\net9.0-windows\win-x64\publish\
echo.

echo Files generated:
dir "WAload\bin\Release\net9.0-windows\win-x64\publish" /b

echo.
echo ========================================
echo SETUP PROJECT INTEGRATION
echo ========================================
echo.
echo For Visual Studio Setup Project:
echo 1. Open your Setup Project in Visual Studio
echo 2. Right-click on "Application Folder" in the File System view
echo 3. Select "Add" -> "Project Output..."
echo 4. Choose "WAload" project and "Primary Output"
echo 5. Or manually add the files from the publish directory
echo.
echo Main executable: WAload.exe
echo Required folders: ffmpeg\, Node\
echo.
echo ========================================
echo Ready for setup project integration!
echo ========================================
pause 