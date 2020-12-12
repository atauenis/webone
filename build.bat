@rem Requires https://github.com/qmfrederik/dotnet-packaging/
@rem Remember to run `dotnet tool update --global dotnet-rpm/deb/zip/tarball`
@echo Building WebOne for Debian/Ubuntu, RedHat/CentOS, macOS and Windows...
rmdir bin\Release /S
rmdir bin\ReleaseSC /S
dotnet restore
dotnet publish -c Release -r linux-x64
dotnet deb --no-restore -c Release -r linux-x64
dotnet rpm --no-restore -c Release -r linux-x64
dotnet publish -c ReleaseSC -r linux-arm
dotnet deb --no-restore -c ReleaseSC -r linux-arm
dotnet rpm --no-restore -c ReleaseSC -r linux-arm
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
@echo All platforms and kinds of packages are processed.