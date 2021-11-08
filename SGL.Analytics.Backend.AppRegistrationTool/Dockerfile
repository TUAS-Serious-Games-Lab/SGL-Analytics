#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Security/SGL.Analytics.Backend.Security.csproj", "SGL.Analytics.Backend.Security/"]
COPY ["SGL.Analytics.Backend.Utilities/SGL.Analytics.Backend.Utilities.csproj", "SGL.Analytics.Backend.Utilities/"]
COPY ["SGL.Analytics.Utilities/SGL.Analytics.Utilities.csproj", "SGL.Analytics.Utilities/"]
COPY ["SGL.Analytics.Backend.Users.Application/SGL.Analytics.Backend.Users.Application.csproj", "SGL.Analytics.Backend.Users.Application/"]
COPY ["SGL.Analytics.Backend.Logs.Application/SGL.Analytics.Backend.Logs.Application.csproj", "SGL.Analytics.Backend.Logs.Application/"]
COPY ["SGL.Analytics.Backend.Logs.Infrastructure/SGL.Analytics.Backend.Logs.Infrastructure.csproj", "SGL.Analytics.Backend.Logs.Infrastructure/"]
COPY ["SGL.Analytics.Backend.Users.Infrastructure/SGL.Analytics.Backend.Users.Infrastructure.csproj", "SGL.Analytics.Backend.Users.Infrastructure/"]
COPY ["SGL.Analytics.Backend.AppRegistrationTool/SGL.Analytics.Backend.AppRegistrationTool.csproj", "SGL.Analytics.Backend.AppRegistrationTool/"]
COPY ["NuGet.config", "NuGet.config"]
ARG SGL_PKG_USER
ARG SGL_PKG_TOKEN
RUN echo 'dotnet nuget update source SGL --username ${SGL_PKG_USER} --password ${SGL_PKG_TOKEN} --store-password-in-clear-text' > pkg-login.sh &&\
	chmod +x pkg-login.sh && ./pkg-login.sh &&\
	dotnet restore "SGL.Analytics.Backend.AppRegistrationTool/SGL.Analytics.Backend.AppRegistrationTool.csproj" &&\
	dotnet nuget remove source SGL && rm pkg-login.sh
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.AppRegistrationTool"
RUN dotnet build "SGL.Analytics.Backend.AppRegistrationTool.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.AppRegistrationTool.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/definitions
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.AppRegistrationTool.dll"]
CMD ["push","/app/definitions"]
