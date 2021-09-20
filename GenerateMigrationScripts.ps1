cd SGL.Analytics.Backend.Logs.Infrastructure
dotnet ef migrations script --startup-project ..\SGL.Analytics.Backend.Logs.Collector\ --idempotent --output DbMigrations.sql
cd ../SGL.Analytics.Backend.Users.Infrastructure
dotnet ef migrations script --startup-project ..\SGL.Analytics.Backend.Users.Registration\ --idempotent --output DbMigrations.sql
