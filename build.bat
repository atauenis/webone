@echo WebOne build script	25.12.2021
@echo.

@echo Clean up directories:
@rmdir bin\Release /S
@rmdir bin\ReleaseSC /S
@echo.

@echo Checking for packaging toolkit updates...
@dotnet tool update --global dotnet-zip
@IF ERRORLEVEL 1 GOTO NoToolkit
@IF ERRORLEVEL 9009 GOTO NoNetSDK
@dotnet tool update --global dotnet-deb
@IF ERRORLEVEL 1 GOTO NoToolkit
@dotnet tool update --global dotnet-rpm
@IF ERRORLEVEL 1 GOTO NoToolkit
@dotnet tool update --global dotnet-tarball
@IF ERRORLEVEL 1 GOTO NoToolkit
@echo.

:Build
@echo Building WebOne for Debian/Ubuntu, RedHat/CentOS, macOS and Windows...
dotnet restore
dotnet publish -c Release -r linux-x64
dotnet deb --no-restore -c Release -r linux-x64
dotnet rpm --no-restore -c Release -r linux-x64
dotnet publish -c ReleaseSC -r linux-arm
dotnet deb --no-restore -c ReleaseSC -r linux-arm
dotnet rpm --no-restore -c ReleaseSC -r linux-arm
dotnet publish -c ReleaseSC -r linux-arm64
dotnet deb --no-restore -c ReleaseSC -r linux-arm64
dotnet rpm --no-restore -c ReleaseSC -r linux-arm64
dotnet publish -c Release -r osx-x64
@rem dotnet pkg -r osx-x64 -c Release
dotnet tarball --no-restore -c Release -r osx-x64
dotnet publish -c Release -r win-x86
dotnet zip --no-restore -c Release -r win-x86
dotnet zip --no-restore -c ReleaseSC -r win-x86
dotnet publish -c Release -r win-arm
dotnet zip --no-restore -c Release -r win-arm
dotnet publish -c Release -r win-x64
dotnet zip --no-restore -c Release -r win-x64
@rem Win32 build must be last because else VS debugging will be broken.
@echo.
@echo All platforms and kinds of packages are processed.
@echo.
@GOTO :EOF

:NoToolkit
@echo BUILD PROBLEM:
@echo Your environment does not have Packaging utilities for .NET Core installed.
@echo Press any key to install them and try again, or Ctrl+C to exit this script now.
@echo.
@pause
dotnet tool install --global dotnet-zip
dotnet tool install --global dotnet-tarball
dotnet tool install --global dotnet-rpm
dotnet tool install --global dotnet-deb
dotnet zip install
dotnet tarball install
dotnet rpm install
dotnet deb install
@IF ERRORLEVEL 0 GOTO Build
@echo Seems, an error occured. How about to read this:
@echo https://github.com/quamotion/dotnet-packaging  ?
@GOTO :EOF

:NoNetSDK
@echo ERROR:
@echo You need to have Microsoft .NET Core SDK 3.1 or Microsoft Visual Studio 2019+.
@echo Download it here: https://dotnet.microsoft.com/en-us/download/dotnet/3.1
@echo          or here: https://visualstudio.microsoft.com/ru/vs/community/