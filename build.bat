@rem Requires https://github.com/qmfrederik/dotnet-packaging/
@echo Building WebOne for Debian/Ubuntu, RedHat/CentOS and Windows...
rmdir bin\Release /S
dotnet restore
dotnet publish -r linux-x64 -c Release --self-contained false
dotnet deb --no-restore -c Release -r linux-x64
dotnet rpm --no-restore -c Release -r linux-x64
dotnet publish -r linux-arm -c Release --self-contained false
dotnet deb --no-restore -c Release -r linux-arm
dotnet rpm --no-restore -c Release -r linux-arm
@rem dotnet publish -r osx-x64 -c Release --self-contained false
@rem dotnet pkg -r osx-x64 -c Release
dotnet publish -r win-x86 -c ReleaseWin32 --self-contained false
dotnet zip --no-restore -c ReleaseWin32 -r win-x86
@rem Win32 build must be last because else VS debugging will be broken.
@echo All platforms and kinds of packages are processed.