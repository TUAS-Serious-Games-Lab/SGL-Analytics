# Tutorial on integrating SGL Analytics

## Instantiating the Client

To use the SGL Analytics client in a game application, one needs to reference the `SGL.Analytics.Client` NuGet package or the assemblies provided by it and then instantiate an object of the class SGLAnalytics.
The simplest way to do this instantiation is probably inside a Singleton game object.
When the default implementations for swappable components are used, only the first three parameters need to be used:
```csharp
private const string APP_NAME = "Tutorial";
private const string APP_API_TOKEN = "qRGe0vVwDD+97Nc3V7YIDXpm2WcokEyRdwIdbHwlzHw=";
private const string BACKEND_BASE_URL = "https://shg-test.hochschule-trier.de";
private SGLAnalytics analytics;

void Init(){
	analytics = new SGLAnalytics(APP_NAME, APP_API_TOKEN, new Uri(BACKEND_BASE_URL));
}
```

During development it may be useful to additionally provide a suitable logger object to use for technical logging by SGL Analytics:
```csharp
ILogger<SGLAnalytics> logger = LoggerFactory.Create(config => { /* Setup providers */ }).CreateLogger<SGLAnalytics>();
// ...
analytics = new SGLAnalytics(APP_NAME, APP_API_TOKEN, new Uri(BACKEND_BASE_URL), diagnosticsLogger: logger);
```

If user registration data should be stored using e.g. an engine-specific implementation of `IRootDataStore`, such an implementation object can be provided as follows:
```csharp
IRootDataStore ds = // Instantiate implementation class here
// ...
analytics = new SGLAnalytics(APP_NAME, APP_API_TOKEN, new Uri(BACKEND_BASE_URL), rootDataStore: ds);
```

## SGL Analytics Lifecycle

### Application Startup

### Game Session Begin

### Optional: Game Session End

### Application Shutdown

### Optional: Retrying Uploads Explicitly

## Recording Entries

### Game Logic Events

### State Snapshots
