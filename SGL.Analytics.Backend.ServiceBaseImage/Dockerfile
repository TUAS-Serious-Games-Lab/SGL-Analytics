FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS final
RUN apt-get update && apt-get -y install wait-for-it postgresql-client && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443
