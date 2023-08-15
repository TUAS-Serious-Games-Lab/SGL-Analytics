
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Logging.FileLogging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddKeyPerFile(builder.Configuration.GetValue<string>("Jwt:KeyDirectory") ?? "./JWT-Key", optional: true, reloadOnChange: true);
var additionalConfFiles = new List<string>();
builder.Configuration.GetSection("AdditionalConfigFiles").Bind(additionalConfFiles);
foreach (var acf in additionalConfFiles) {
	Console.WriteLine($"Including additional config file {acf}");
	builder.Configuration.AddJsonFile(acf, optional: true, reloadOnChange: true);
}
builder.Logging.AddFile(builder => {
	builder.AddRequestScopePlaceholders();
	builder.AddUserIdScopePlaceholder();
	builder.AddAppNameScopePlaceholder();
});
builder.WebHost.UseStartup<Startup>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
