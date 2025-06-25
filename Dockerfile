FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["WebOne.csproj", "."]
RUN dotnet restore "WebOne.csproj"
COPY . .
RUN dotnet build "WebOne.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebOne.csproj" -c Release -o /app/publish

FROM base AS final
ENV DEBIAN_FRONTEND noninteractive
RUN apt-get update && \
    apt-get install -y python3-pip imagemagick imagemagick-6-common ffmpeg && \
    python3 -m pip install -U yt-dlp
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "webone.dll"]
