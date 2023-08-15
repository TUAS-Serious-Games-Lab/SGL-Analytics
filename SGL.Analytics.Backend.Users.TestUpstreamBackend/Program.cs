using Prometheus;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
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
builder.Services.Configure<FileLoggingProviderOptions>(config => {
	config.Constants.TryAdd("ServiceName", "SGL.Analytics.Test.Upstream");
});

builder.Services.UseJwtLoginService(builder.Configuration);
builder.Services.UseJwtExplicitTokenService(builder.Configuration);
builder.Services.UseJwtBearerAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options => {
	options.AddPolicy("AuthenticatedAppUser", p => p.RequireClaim("userid").RequireClaim("appname"));
	options.DefaultPolicy = options.GetPolicy("AuthenticatedAppUser") ?? throw new InvalidOperationException("Couldn't find AuthenticatedAppUser policy.");
});

builder.Services.AddHealthChecks().ForwardToPrometheus();

DiagnosticSourceAdapter.StartListening();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
	app.UseDeveloperExceptionPage();
}
else {
	app.UseLoggingExceptionHandler<Program>();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints => {
	endpoints.MapControllers();
	endpoints.MapHealthChecks("/health").RequireHost($"localhost:{app.Configuration["ManagementPort"]}");
	endpoints.MapMetrics().RequireHost($"*:{app.Configuration["ManagementPort"]}");
});

app.Run();
