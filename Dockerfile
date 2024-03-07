#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MidpassAutoQueue.csproj", "."]
RUN dotnet restore "./MidpassAutoQueue.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./MidpassAutoQueue.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MidpassAutoQueue.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER root
RUN apt-get update && apt-get -y upgrade
RUN apt-get install -y wget
RUN wget -q https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb
RUN apt-get update
RUN apt-get install -y powershell
RUN pwsh /app/playwright.ps1 install chromium --with-deps
ENTRYPOINT ["dotnet", "MidpassAutoQueue.dll"]