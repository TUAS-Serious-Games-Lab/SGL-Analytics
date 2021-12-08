# Tutorial on integrating SGL Analytics

## Instantiating the Client

To use the SGL Analytics client in a game application, one needs to reference the `SGL.Analytics.Client` NuGet package or the assemblies provided by it and then instantiate an object of the class SGLAnalytics.
The simplest way to do this instantiation is probably inside a Singleton game object.
When the default implementations for swappable components are used, only the first three parameters need to be used:
```csharp
private const string APP_NAME = "Tutorial"; // A unique name to identify the application
                                            // Used for identification with the backend and to generate the local default
                                            // storage directory under the user's AppData directory.
private const string APP_API_TOKEN = "qRGe0vVwDD+97Nc3V7YIDXpm2WcokEyRdwIdbHwlzHw="; // Specific for the app
                                                                                     // Can be generated using SGL.Analytics.Backend.AppRegistrationTool.
private const string BACKEND_BASE_URL = "https://shg-test.hochschule-trier.de"; // Address of the backend server to use
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

## Backend Configuration

For the application to be able to use a SGLAnalytics backend server, it must be registered there first.
This is done using the `SGL.Analytics.Backend.AppRegistrationTool` executable that registers the apps described by JSON files in a given directory.
These files specify the app name and app API token as used above, and they can furthermore specify a set of application-specific user properties to take upon user registration.
An example for such a file is given below.
```json
{
  "Name": "Example",
  "ApiToken": "TLUiAhePnt/rKLUvVBdtUt/lQIgqBKskq4iW9aNEyJY=",
  "UserProperties": [
    {
      "Name": "Age",
      "Type": "Integer",
      "Required": true
    },
    {
      "Name": "SomeText",
      "Type": "String",
      "Required": false
    },
    {
      "Name": "SomeStructuredValue",
      "Type": "Json",
      "Required": false
    }
  ]
}
```

The valid options for the `Type` field are defined in `SGL.Analytics.Backend.Domain.Entity.UserPropertyType`.

## SGL Analytics Lifecycle

There are a few places in the game lifecycle where the application needs to perform operations with SGL Analytics.

### User Setup

At some point in time between launching the game application and starting a game session, the application shall check whether the user is registered with SGL Analytics by calling `analytics.IsRegistered()`.
If they are not yet registered, the application shall display a data collection consent dialog and (if applicable to the app) prompt the user for application-specific registration data.
When the user agrees (and has filled out the registration data), the application shall register the user by calling and awaiting `analytics.RegisterAsync` with an object of a user data class derived from `BaseUserData`, containing the registration data.
The application should catch exceptions from the register operation, in case it fails.
Most possible errors come from technical problems, like no network connection being available or a problem with the backend server.
However, apps that use the optional username property of `BaseUserData` should specifically catch `UsernameAlreadyTakenException`, which is thrown when the chosen username is already registered with the application.
As usernames need to be unique per application, the game app should ask the user to pick a different name and remember the other registration data values to retry with the new username.

### Game Session Begin

When a new game session is started (typically when the user clicks 'New Game' or similar), the game app should call `analytics.StartNewLog()` to start a log file for the starting session.
A log file needs to be active, before entries can be recorded, thus at least one call to `analytics.StartNewLog()` needs to be made.
Starting a new log file for each session is however recommended for most cases.
When `analytics.StartNewLog()` starts a new log and there already was an active one, the old log continues being flushed to disk in the background and is added to the upload queue when the background writing is complete.

### Application Shutdown

Before the game application is shut down, it needs to call and await `analytics.FinishAsync()`.
This closes the currently active log (and possibly preceeding ones) by flushing the in-memory buffer to the file(s), ensuring that the closing `]` is written to make the files valid JSON, and then closing the file(s).
The completed files are then added to the upload queue.
The asynchronous operation started by `analytics.FinishAsync()` only finishes when all buffered log files are flushed and the background upload task has also worked through its queue, i.e. when all pending log files have either been uploaded to the backend or failed their upload.
In the latter case they are usually kept locally and their upload is queued for retrying upon instantiating `SGLAnalytics`.
The only case where the upload is not retried is when the server rejected the upload because the file was bigger than the configured size limit. Retrying those files would only waste the user's network bandwidth, just to fail again.

### Optional: Game Session End

When the user exists a game session to the menu, the game can optionally call and await `analytics.FinishAsync()` instead of waiting until application shutdown.
This closes the current log file, flushes it and preceeding ones to disk and waits for it and preceeding files to be uploaded.
To resume SGL Analytics operation, the app then needs to call `analytics.StartNewLog()` again to start a new file.
Not finishing before exiting the game session is not very problematic, it only keeps the log file active until either a new one is started, or the application is shut down.
If users tend to leave the game sitting in the menu it might be advantageous to call finish.

### Optional: Retrying Uploads Explicitly

Usually, log files that previously failed their upload are kept locally and their upload is retried upon next startup, when `SGLAnalytics` is instantiated with a user registered.
Files are also kept locally if logs are recorded before the user is registered.
In this case, the uploads of existing files are queued when the registration is completed.

As a typical reason for failing uploads is that the client device currently has no network connection, client applications can explicitly retry uploads either after some time (e.g. after a few minutes) or when it has reason to assume that the device now has a network connection, e.g. because it just established a connection to some other service it uses, e.g. a multiplayer server.
Queueing upload retries is done by simply calling `analytics.StartRetryUploads()`.
The application should however **not** frequently trigger retries as this would consume a non-trivial amount of ressources on both, the client and the backend.

## Recording Entries

During normal gameplay, entries are recorded to the currently active log by calling the appropriate `Record`... methods from the gameplay code.
The data associated with the entries are provided by the gameplay code as objects.
The method parameters for this are specified as `object` to allow a broad range of types.
However, there is one central requirement for these objects: They need to be convertable to JSON using the `System.Text.Json` serialization.
This includes [collections like dictionaries](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-supported-collection-types), but custom classes are also supported by serializing the public non-static properties of the class.
For more complex data objects, the classes of the provided objects can also specify the `[JsonConverter(typeof(SomeCustomConverter))]` attribute to define their [own conversion logic](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to) in `SomeCustomConverter`.
See [the `System.Text.Json` manual](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview) for details.

The entries are categorized into named channels to later allow easier filtering of the file contents.
All entries also automatically include the timestamp of when the recording method was called, which allows mesauring time between events and associates snapshots with their recording time.

### Game Logic Events

Examples for game logic events are:
- Starting a game
- Completing a level / stage
- Losing a level / stage
- Picking up an item
- Buying / selling an item from / to another player / to an NPC vendor
- Using certain abilities
- Consuming items
- Rolling a dice and then making a game move
- Drawing or playing a card
- Starting or completing a puzzle / minigame

Such events are recorded by calling the methods `RecordEvent` and `RecordEventUnshared` on the `analytics` object.
In many cases, the event object is created specifically for the call. In this case, `RecordEventUnshared` should be used.
`RecordEvent` is required for cases where the event object is kept by the calling code and may be modified later.
As the events are written to disk asynchronously with the calling code, later modification would interfere with the writing process.
To prevent this problem, `RecordEvent` copies the passed object before applying the same logic as `RecordEventUnshared` and thus requires the object to implement `ICloneable` as a deep copy of all mutable subobjects.

The event type field of the recorded entry can be specified using one of the following ways:
- If an overload of `RecordEvent` and `RecordEventUnshared` that takes an `eventType` parameter is called, the value of the parameter is used.
- If the dynamic type of the event object has an `[EventType]` attribute, its `EventTypeName` property is used.
- Otherwise, the type name of the dynamic type of the event object is used.

A sketch for a snippet of gameplay code that defines and then records events could for example look like this:
- The relevant definition:
```csharp
enum Suit { Diamonds, Clubs, Hearts, Spades };
enum Rank { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King };

[EventType("CardDrawn")]
class CardDrawnEvent {
	public Suit Suit {get; set;}
	public Rank Rank {get; set;}
}

[EventType("CardPlayed")]
class CardPlayedEvent {
	public Suit Suit {get; set;}
	public Rank Rank {get; set;}
}

[EventType("Won")]
class WonEvent {
	public int Points {get; set;}
}

[EventType("Lost")]
class LostEvent {
	public int Points {get; set;}
	public int WinnersPoints {get; set;}
}

[EventType("Tie")]
class TieEvent {
	public int Points {get; set;}
}
```
- The game logic code:
```csharp
var drawn_card = gameCore.DrawCardFromStack();
playerHand.Add(drawn_card);
analytics.RecordEventUnshared("DrawnCards",new CardDrawnEvent(){ Suit = drawn_card.Suit, Rank = drawn_card.Rank });

// player selects card to play
var played_card = ui.GetPlayerSelection();
analytics.RecordEventUnshared("PlayedCards", new CardPlayedEvent(){ Suit = played_card.Suit, Rank = played_card.Rank });
var outcome = gameCore.PlayCardAndFinishRound(played_card);
if(outcome == Outcome.Won){
	analytics.RecordEventUnshared("Outcomes", new WonEvent(){Points = GetMyPoints()});
	DisplayWonScreen();
}
else if (outcome == Outcome.Lost){
	analytics.RecordEventUnshared("Outcomes", new LostEvent(){ Points = GetMyPoints(), WinnersPoints = gameCore.GetWinner().Points });
	DisplayLostScreen();
}
else if (outcome == Outcome.Tie){
	analytics.RecordEventUnshared("Outcomes", new TieEvent(){ Points = GetMyPoints() });
	DisplayTieScreen();
}
```

### State Snapshots
