#!/bin/bash
# Edit WebOne.csproj file to use net8.0 instead of net6.0 before run.
script_dir=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
docker run --pull always --rm -v ${script_dir}:/src -w /src taphome/dotnet-armv6:latest dotnet publish WebOne.csproj -r linux-armv6 --self-contained true -c Release_SC -f net8.0 -o bin/armv6 -t:CreateDeb
