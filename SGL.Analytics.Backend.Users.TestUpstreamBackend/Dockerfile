#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
RUN apk add --no-cache curl 
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Users.Application/SGL.Analytics.Backend.Users.Application.csproj", "SGL.Analytics.Backend.Users.Application/"]
COPY ["SGL.Analytics.Backend.Users.TestUpstreamBackend/SGL.Analytics.Backend.Users.TestUpstreamBackend.csproj", "SGL.Analytics.Backend.Users.TestUpstreamBackend/"]
COPY ["NuGet.config", "NuGet.config"]
ARG SGL_PKG_USER
ARG SGL_PKG_TOKEN
RUN echo 'dotnet nuget update source SGL --username ${SGL_PKG_USER} --password ${SGL_PKG_TOKEN} --store-password-in-clear-text' > pkg-login.sh &&\
	chmod +x pkg-login.sh && ./pkg-login.sh &&\
	dotnet restore --disable-parallel "SGL.Analytics.Backend.Users.TestUpstreamBackend/SGL.Analytics.Backend.Users.TestUpstreamBackend.csproj" &&\
	dotnet nuget remove source SGL && rm pkg-login.sh
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.Users.TestUpstreamBackend"
RUN dotnet build --no-restore "SGL.Analytics.Backend.Users.TestUpstreamBackend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish --no-restore "SGL.Analytics.Backend.Users.TestUpstreamBackend.csproj" -c Release -o /app/publish

FROM ${SERVICE_BASE_IMAGE_OVERRIDE:-base} AS final
HEALTHCHECK --start-period=30s CMD curl --fail --no-progress-meter http://localhost:8080/health || exit 1
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.Users.TestUpstreamBackend.dll"]
