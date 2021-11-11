FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine AS final
ADD https://raw.githubusercontent.com/eficode/wait-for/7761def579a37f9160aa60b73984531b26479993/wait-for /bin/wait-for
RUN apk add --no-cache postgresql-client && chmod +x /bin/wait-for
WORKDIR /app
EXPOSE 80
EXPOSE 443
