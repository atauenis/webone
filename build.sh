#!/bin/bash
dotnet restore
dotnet publish -c Release -r linux-x64 -t:CreateDeb,CreateRpm,Clean
dotnet publish -c ReleaseSC -r linux-arm -t:CreateDeb,CreateRpm,Clean
dotnet publish -c ReleaseSC -r linux-arm64 -t:CreateDeb,CreateRpm,Clean
dotnet publish -c Release -r osx-x64 -t:CreateZip,Clean
dotnet publish -c Release -r osx-arm64 -t:CreateZip,Clean
dotnet publish -c Release -r win-x86 -t:CreateZip,Clean
dotnet publish -c ReleaseSC -r win-x86 -t:CreateZip,Clean
dotnet publish -c Release -r win-arm -t:CreateZip,Clean
dotnet publish -c Release -r win-x64 -t:CreateZip,Clean