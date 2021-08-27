#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SGL.Analytics.Backend.UserDB/SGL.Analytics.Backend.UserDB.csproj", "SGL.Analytics.Backend.UserDB/"]
COPY ["SGL.Analytics.DTO/SGL.Analytics.DTO.csproj", "SGL.Analytics.DTO/"]
COPY ["SGL.Analytics.Backend.Model/SGL.Analytics.Backend.Model.csproj", "SGL.Analytics.Backend.Model/"]
RUN dotnet restore "SGL.Analytics.Backend.UserDB/SGL.Analytics.Backend.UserDB.csproj"
COPY . .
WORKDIR "/src/SGL.Analytics.Backend.UserDB"
RUN dotnet build "SGL.Analytics.Backend.UserDB.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SGL.Analytics.Backend.UserDB.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGL.Analytics.Backend.UserDB.dll"]
