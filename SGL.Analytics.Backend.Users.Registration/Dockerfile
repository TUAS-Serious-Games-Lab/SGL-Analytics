#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
RUN apt-get update && apt-get -y install wait-for-it postgresql-client && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Security/SGL.Analytics.Backend.Security.csproj", "SGL.Analytics.Backend.Security/"]
COPY ["SGL.Analytics.Backend.Utilities/SGL.Analytics.Backend.Utilities.csproj", "SGL.Analytics.Backend.Utilities/"]
COPY ["SGL.Analytics.Utilities/SGL.Analytics.Utilities.csproj", "SGL.Analytics.Utilities/"]
COPY ["SGL.Analytics.Utilities.Logging/SGL.Analytics.Utilities.Logging.csproj", "SGL.Analytics.Utilities.Logging/"]
COPY ["SGL.Analytics.Backend.WebUtilities/SGL.Analytics.Backend.WebUtilities.csproj", "SGL.Analytics.Backend.WebUtilities/"]
COPY ["SGL.Analytics.Backend.Users.Application/SGL.Analytics.Backend.Users.Application.csproj", "SGL.Analytics.Backend.Users.Application/"]
COPY ["SGL.Analytics.Backend.Users.Infrastructure/SGL.Analytics.Backend.Users.Infrastructure.csproj", "SGL.Analytics.Backend.Users.Infrastructure/"]
COPY ["SGL.Analytics.Backend.Users.Registration/SGL.Analytics.Backend.Users.Registration.csproj", "SGL.Analytics.Backend.Users.Registration/"]
RUN dotnet restore "SGL.Analytics.Backend.Users.Registration/SGL.Analytics.Backend.Users.Registration.csproj"
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.Users.Registration"
RUN dotnet build "SGL.Analytics.Backend.Users.Registration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.Users.Registration.csproj" -c Release -o /app/publish
RUN dotnet tool install --global dotnet-ef &&\
	export PATH="$PATH:/root/.dotnet/tools" && \
	cd ../SGL.Analytics.Backend.Users.Infrastructure && \
	dotnet ef migrations script --startup-project ../SGL.Analytics.Backend.Users.Registration --idempotent --output /app/publish/DbMigrations.sql

FROM ${SERVICE_BASE_IMAGE_OVERRIDE:-base} AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.Users.Registration.dll"]
