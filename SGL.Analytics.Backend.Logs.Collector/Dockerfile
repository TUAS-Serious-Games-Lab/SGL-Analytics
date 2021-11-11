#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine AS base
ADD https://raw.githubusercontent.com/eficode/wait-for/7761def579a37f9160aa60b73984531b26479993/wait-for /bin/wait-for
RUN apk add --no-cache postgresql-client && chmod +x /bin/wait-for
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Logs.Application/SGL.Analytics.Backend.Logs.Application.csproj", "SGL.Analytics.Backend.Logs.Application/"]
COPY ["SGL.Analytics.Backend.Logs.Infrastructure/SGL.Analytics.Backend.Logs.Infrastructure.csproj", "SGL.Analytics.Backend.Logs.Infrastructure/"]
COPY ["SGL.Analytics.Backend.Logs.Collector/SGL.Analytics.Backend.Logs.Collector.csproj", "SGL.Analytics.Backend.Logs.Collector/"]
COPY ["NuGet.config", "NuGet.config"]
ARG SGL_PKG_USER
ARG SGL_PKG_TOKEN
RUN echo 'dotnet nuget update source SGL --username ${SGL_PKG_USER} --password ${SGL_PKG_TOKEN} --store-password-in-clear-text' > pkg-login.sh &&\
	chmod +x pkg-login.sh && ./pkg-login.sh &&\
	dotnet restore "SGL.Analytics.Backend.Logs.Collector/SGL.Analytics.Backend.Logs.Collector.csproj" &&\
	dotnet nuget remove source SGL && rm pkg-login.sh
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.Logs.Collector"
RUN dotnet build "SGL.Analytics.Backend.Logs.Collector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.Logs.Collector.csproj" -c Release -o /app/publish
RUN cd ../SGL.Analytics.Backend.Logs.Infrastructure && \
	dotnet ef migrations script --startup-project ../SGL.Analytics.Backend.Logs.Collector --idempotent --output /app/DbMigrations.sql &&\
	../remove_BOM.sh /app/DbMigrations.sql

FROM ${SERVICE_BASE_IMAGE_OVERRIDE:-base} AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /app/DbMigrations.sql /
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.Logs.Collector.dll"]
