#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
RUN apt-get update && apt-get -y install wait-for-it postgresql-client && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Users.Application/SGL.Analytics.Backend.Users.Application.csproj", "SGL.Analytics.Backend.Users.Application/"]
COPY ["SGL.Analytics.Backend.Users.Infrastructure/SGL.Analytics.Backend.Users.Infrastructure.csproj", "SGL.Analytics.Backend.Users.Infrastructure/"]
COPY ["SGL.Analytics.Backend.Users.Registration/SGL.Analytics.Backend.Users.Registration.csproj", "SGL.Analytics.Backend.Users.Registration/"]
COPY ["NuGet.config", "NuGet.config"]
ARG SGL_PKG_USER
ARG SGL_PKG_TOKEN
RUN echo 'dotnet nuget update source SGL --username ${SGL_PKG_USER} --password ${SGL_PKG_TOKEN} --store-password-in-clear-text' > pkg-login.sh &&\
	chmod +x pkg-login.sh && ./pkg-login.sh &&\
	dotnet restore "SGL.Analytics.Backend.Users.Registration/SGL.Analytics.Backend.Users.Registration.csproj" &&\
	dotnet nuget remove source SGL && rm pkg-login.sh
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.Users.Registration"
RUN dotnet build "SGL.Analytics.Backend.Users.Registration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.Users.Registration.csproj" -c Release -o /app/publish
RUN cd ../SGL.Analytics.Backend.Users.Infrastructure && \
	dotnet ef migrations script --startup-project ../SGL.Analytics.Backend.Users.Registration --idempotent --output /app/publish/DbMigrations.sql &&\
	../remove_BOM.sh /app/publish/DbMigrations.sql

FROM ${SERVICE_BASE_IMAGE_OVERRIDE:-base} AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.Users.Registration.dll"]
