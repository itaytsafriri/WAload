
# WAload Setup Project Guide

## Overview
This guide explains how to configure WAload for self-contained deployment and integrate it with a Visual Studio Setup Project.

## Self-Contained Configuration

### What is Self-Contained?
A self-contained application includes the .NET runtime and all dependencies, so users don't need to install .NET separately. This makes deployment much easier.

### Configuration Applied
The project has been configured with the following settings:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishTrimmed>false</PublishTrimmed>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

## Building the Self-Contained Application

### Option 1: Using the Batch File (Recommended)
1. Run `build_self_contained.bat` in the project root
2. This will automatically clean, restore, and build the application
3. Output will be in: `WAload\bin\Release\net9.0-windows\win-x64\publish\`

### Option 2: Using Visual Studio
1. Right-click on the WAload project in Solution Explorer
2. Select "Publish..."
3. Choose "Folder" as the target
4. Click "Browse" and select the publish directory
5. In Advanced Settings:
   - Set "Deployment mode" to "Self-contained"
   - Set "Target runtime" to "win-x64"
   - Set "File publish options" to "Produce single file"
6. Click "Publish"

### Option 3: Using Command Line
```bash
dotnet publish WAload\WAload.csproj --configuration Release --runtime win-x64 --self-contained true --output "WAload\bin\Release\net9.0-windows\win-x64\publish"
```

## Visual Studio Setup Project Integration

### Step 1: Create Setup Project
1. In Visual Studio, go to File → New → Project
2. Search for "Setup Project" or "InstallShield Limited Edition"
3. Create a new setup project

### Step 2: Add Application Files
1. In the Setup Project, open "File System" view
2. Right-click on "Application Folder"
3. Select "Add" → "Project Output..."
4. Choose "WAload" project and "Primary Output"
5. This will automatically include the main executable

### Step 3: Add Required Folders
Since the application includes Node.js and FFmpeg, you need to add these folders:

1. Right-click on "Application Folder"
2. Select "Add" → "Folder"
3. Create folders for:
   - `ffmpeg` (contains FFmpeg executables)
   - `Node` (contains Node.js runtime and scripts)

### Step 4: Add Files to Folders
1. Navigate to the publish directory: `WAload\bin\Release\net9.0-windows\win-x64\publish\`
2. Copy the contents of `ffmpeg\` folder to your setup project's `ffmpeg` folder
3. Copy the contents of `Node\` folder to your setup project's `Node` folder

### Step 5: Configure Setup Properties
1. Right-click on the Setup Project in Solution Explorer
2. Select "Properties"
3. Configure:
   - **Product Name**: WAload
   - **Manufacturer**: Your Company Name
   - **Product Version**: 1.0.0
   - **Description**: WhatsApp Media Downloader

### Step 6: Add Shortcuts
1. In File System view, right-click on "User's Programs Menu"
2. Select "Add" → "Folder" → Name it "WAload"
3. Right-click on the new folder → "Create Shortcut to Primary Output"
4. Name the shortcut "WAload"

### Step 7: Add Desktop Shortcut
1. Right-click on "User's Desktop"
2. Select "Create Shortcut to Primary Output"
3. Name it "WAload"

## File Structure in Setup Project

Your setup project should include:

```
Application Folder/
├── WAload.exe (Primary Output)
├── WAload.pdb (Debug symbols - optional)
├── D3DCompiler_47_cor3.dll
├── PenImc_cor3.dll
├── PresentationNative_cor3.dll
├── vcruntime140_cor3.dll
├── wpfgfx_cor3.dll
├── ffmpeg/
│   ├── ffmpeg.exe
│   ├── ffplay.exe
│   ├── ffprobe.exe
│   └── *.dll files
└── Node/
    ├── node.exe
    ├── package.json
    ├── whatsapp.js
    └── other Node.js files
```

## Build and Test

### Build Setup Project
1. Right-click on the Setup Project
2. Select "Build"
3. The installer will be created in the setup project's output directory

### Test Installation
1. Run the generated .msi file
2. Install the application
3. Verify that WAload.exe runs correctly
4. Check that FFmpeg and Node.js components are accessible

## Troubleshooting

### Common Issues

1. **Missing Dependencies**: Ensure all DLL files from the publish directory are included
2. **FFmpeg Not Found**: Verify the ffmpeg folder is included in the setup
3. **Node.js Errors**: Check that the Node folder contains all required files
4. **Permission Issues**: Ensure the installer has appropriate permissions

### File Size Considerations
- Self-contained applications are larger (100-200MB) but don't require .NET installation
- Consider using framework-dependent deployment if file size is critical
- Use compression options to reduce installer size

## Alternative: Framework-Dependent Deployment

If you prefer smaller file sizes and users have .NET installed:

```xml
<SelfContained>false</SelfContained>
<PublishSingleFile>false</PublishSingleFile>
```

This creates a smaller application but requires .NET 9.0 to be installed on target machines.

## Next Steps

1. Test the self-contained build locally
2. Create your Visual Studio Setup Project
3. Add all required files and folders
4. Build and test the installer
5. Distribute the .msi file to users

The self-contained approach ensures maximum compatibility and ease of deployment for end users. 