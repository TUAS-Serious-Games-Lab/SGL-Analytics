#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
RUN apt-get update && apt-get -y install wait-for-it postgresql-client && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SGL.Analytics.Backend.Logs.Collector/SGL.Analytics.Backend.Logs.Collector.csproj", "SGL.Analytics.Backend.Logs.Collector/"]
COPY ["SGL.Analytics.Backend.Security/SGL.Analytics.Backend.Security.csproj", "SGL.Analytics.Backend.Security/"]
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Utilities/SGL.Analytics.Utilities.csproj", "SGL.Analytics.Utilities/"]
COPY ["SGL.Analytics.Backend.WebUtilities/SGL.Analytics.Backend.WebUtilities.csproj", "SGL.Analytics.Backend.WebUtilities/"]
COPY ["SGL.Analytics.Backend.Logs.Application/SGL.Analytics.Backend.Logs.Application.csproj", "SGL.Analytics.Backend.Logs.Application/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Logs.Infrastructure/SGL.Analytics.Backend.Logs.Infrastructure.csproj", "SGL.Analytics.Backend.Logs.Infrastructure/"]
RUN dotnet restore "SGL.Analytics.Backend.Logs.Collector/SGL.Analytics.Backend.Logs.Collector.csproj"
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.Logs.Collector"
RUN dotnet build "SGL.Analytics.Backend.Logs.Collector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.Logs.Collector.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.Logs.Collector.dll"]