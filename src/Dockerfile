FROM mcr.microsoft.com/dotnet/framework/sdk:4.8-windowsservercore-ltsc2019 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .
COPY JabbR/*.csproj ./JabbR/
COPY JabbR/*.config ./JabbR/
COPY .nuget .nuget
RUN nuget restore

# copy everything else and build app
COPY JabbR ./JabbR
WORKDIR /app/JabbR
RUN msbuild /p:Configuration=Release

FROM mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2019 AS runtime
SHELL ["C:\\LogMonitor\\LogMonitor.exe", "powershell.exe"]

WORKDIR /LogMonitor
COPY ./logmonitor/* ./

WORKDIR /inetpub/wwwroot
COPY --from=build /app/JabbR/. ./


ENTRYPOINT ["powershell.exe", "-File", "c:\\inetpub\\wwwroot\\Startup.ps1"]
