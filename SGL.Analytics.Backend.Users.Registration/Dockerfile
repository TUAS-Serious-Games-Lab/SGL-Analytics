#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SGL.Analytics.Backend.Users.Registration/SGL.Analytics.Backend.Users.Registration.csproj", "SGL.Analytics.Backend.Users.Registration/"]
COPY ["SGL.Analytics.Backend.Security/SGL.Analytics.Backend.Security.csproj", "SGL.Analytics.Backend.Security/"]
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Utilities/SGL.Analytics.Utilities.csproj", "SGL.Analytics.Utilities/"]
COPY ["SGL.Analytics.Backend.Users.Infrastructure/SGL.Analytics.Backend.Users.Infrastructure.csproj", "SGL.Analytics.Backend.Users.Infrastructure/"]
COPY ["SGL.Analytics.Backend.Domain/SGL.Analytics.Backend.Domain.csproj", "SGL.Analytics.Backend.Domain/"]
COPY ["SGL.Analytics.Backend.Users.Application/SGL.Analytics.Backend.Users.Application.csproj", "SGL.Analytics.Backend.Users.Application/"]
RUN dotnet restore "SGL.Analytics.Backend.Users.Registration/SGL.Analytics.Backend.Users.Registration.csproj"
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.Users.Registration"
RUN dotnet build "SGL.Analytics.Backend.Users.Registration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.Users.Registration.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.Users.Registration.dll"]