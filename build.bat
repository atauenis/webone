@rem Requires https://github.com/qmfrederik/dotnet-packaging/
@echo Building WebOne for Debian/Ubuntu, RedHat/CentOS, MacOSX and Windows...
rmdir bin\Release /S
rmdir bin\ReleaseWin32 /S
dotnet restore
dotnet publish -c Release -r linux-x64 --self-contained false
dotnet deb --no-restore -c Release -r linux-x64
dotnet rpm --no-restore -c Release -r linux-x64
dotnet publish -c Release -r linux-arm --self-contained false
dotnet deb --no-restore -c Release -r linux-arm
dotnet rpm --no-restore -c Release -r linux-arm
dotnet publish -c Release -r osx-x64 --self-contained false
@rem dotnet pkg -r osx-x64 -c Release
dotnet zip --no-restore -c Release -r osx-x64
dotnet publish -c ReleaseWin32 -r win-x86 --self-contained false
dotnet zip --no-restore -c ReleaseWin32 -r win-x86
dotnet publish -c ReleaseWin32 -r win-arm --self-contained false
dotnet zip --no-restore -c ReleaseWin32 -r win-arm
dotnet publish -c ReleaseWin32 -r win-x64 --self-contained false
dotnet zip --no-restore -c ReleaseWin32 -r win-x64
@rem Win32 build must be last because else VS debugging will be broken.
@echo All platforms and kinds of packages are processed.