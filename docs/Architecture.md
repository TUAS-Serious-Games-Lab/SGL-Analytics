# Architecture

The SGL Analytics system consists of the following primary components, which will be discussed below:
- Game Frontend
  - Game client library
  - (optionally) Engine-specific adapters (yet to be added at time of writing)
- Backend
  - Log Collector service
  - User Registration service

Additionally, SGL Analytics provides the following administrative tools, described on the linked pages:
- [Application Registration Tool](../SGL.Analytics.Backend.AppRegistrationTool/index.md)
- [Log Repository Garbage Collector](../SGL.Analytics.Backend.Logs.LogRepositoryGC/index.md)

## Game Frontend

### Game Client Library

For gameplay programmers, the client library provides one central class [SGLAnalytics](https://serious-games-lab.pages.gitlab.rlp.net/sgl-analytics/api/SGL.Analytics.Client.SGLAnalytics.html) as the primary point of interaction with SGL Analytics, acting as a facade for the functionality of the library.
Its contructor allows the game code to parameterize and adapt the client for the game's technical environment.
This includes parameters to provide alternative implementations for
- the log storage (defaults to classic file-based storage)
- the user data storage (defaults to a JSON file in the user's application data directory, could e.g. be swapped for integration with the game engine's user data storage)
- the communication with the backend for the log file upload (defaults to REST calls using the [.NET HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient))
- the communication with the backend for the user registration and login (defaults to REST calls using the [.NET HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient))
- a logger for diagnostic information about the SGLAnalytics itself, which is mainly useful during development and testing (defaults to null logger, discarding all logged diagnostics)

The instantiated object then provides methods to
- record events and game-object state snapshots to the current log file
- start a new log file (ending the current one), which is useful to separate game sessions
- check for an existing user registration and register the user if they are not registered yet
- finishing the operations of the `SGLAnalytics` object, which must be called before terminating the game to allow background processes to finish

To minimize the impact of the log recording on the game's performance, the JSON-formatting and writing to disk of the log entries is not done on the game logic thread where the `SGLAnalytics` methods are called.
Instead, the passed data is enqueued into an in-memory queue which is then consumed by a background task (running on the .NET default thread pool) that does the formatting and writes the formatted entries to disk using asynchronous I/O to not block the worker thread while the I/O takes place.

When a log file is ended, it is inserted into another queue that is consumed by another background task which uploads the completed files to the Log Collector service in the backend.
If the upload of a file fails, e.g. because the user's device is currently not connected to the internet, the file remains on the local device. These left-over files are automatically enqueued upon instantiation of an `SGLAnalytics` object. Additionally, they can explictily be re-added to the queue by calling `StartRetryUploads()`, which may be useful if the game application can detect that a network connection should now be present, e.g. after successfully conntecting to the game's own backend service.

To avoid losing yet unwirtten log entries, producing log JSON files with a missing `]` of the root array, or interruping log uploads, the game code must call `FinishAsync` and await its completion to allow the background processes described above to finish. This also closes the current log file without starting a new one. Should the game application crash before the background tasks are finished, the files may be corruped (defective compression blocks, missing entries at the end, incompletely written entries, or missing the `]` which makes them syntactically incorrect as JSON files) or missing (if all their entries were still pending in memory). Everything that was written to disk will however still be uploaded to the backend upon restart. This may allow manual rescuing of the file, e.g. by adding the missing `]`. In general, post-processing scripts should properly handle and report defective files.

It should be noted that because the log-entries are buffered in memory, the rate of recording new entries from the game code must not exceed the available write speed of the target device over a longer time frame. Otherwise, the memory consumption will continue to grow until all available memory is consumed. The system can however absorb short-term spikes of recorded entries if it is allowed to flush the buffer to disk in a lower load period afterwards.

Because the serialization takes place asynchronously on a different thread than the game code, the objects passed to the recording methods must generally not be shared mutable objects, i.e. they must either be immutable (not changed by the game code) or completely handed over to `SGLAnalytics`. The former is typically true for things like string data or data that only changes in the game editor. The latter is true either if the game hands the object over and doesn't do anything with it afterwards, or if the object is created from primitives (and immutables) specifically for the recording operation.
For example, if the game code creates an event object representing the player picking up an item, it can specify the item name, and the coordinates when constructing the object and then pass it to the record method without problems, if it doesn't store the object somewhere else to modify it later.
This is likely the most common way to record entries.
To make it clear, that the objects must not be shared (unless they are immutable), the methods have a name suffix of `Unshared`.
There is however a convenience method `RecordEvent` (without the suffix), that takes a clonable event object, clones it on the calling thread and then enqueues the clone. It is assumed there that the cloning implementation performs a deep copy of at least all mutable objects.
Game code should however prefer the `Unshared` methods to avoid unnecessary copies if the object is unshared or immutable anyways.

## Backend

The backend of SGL Analytics is split into two separate microservices where one handles the actual ingest of log files from the clients and the other one deals with user accounts, i.e. it performs user registrations and session logins based on the registrations.
Besides the usual considerations for microservices, the services are also split to (in principle) allow deployment on separate infrastructure for isolation where the data in the log collector service is supposed to only contain pseudonymous data. If this is actually the case depends on the game code not recording identifying information.
The default deployment as described by the included docker-compose files however puts both services on the same machine behind an API Gateway and thus uses container-level isolation.
To allow separate deployment, the services have disjunct databases, i.e. although both use a table where the known client applications are registered, they don't share that table but have their own copy.

As the backend is designed to handle multiple applications, all API routes take an application name that uniquely identifies the client app and an application API token that acts as a weak first layer of authentication, authenticating the application itself.
The application name is take directly as an API parameter for routes without user authentication and as a claim inside the session security token. In the latter case it is set, when the client logs into the application and then reused for all later calls.

### Log Collector Service

The log collector service provides an API route to upload the log files.
These files are stored in a directory structure that first divided by the client application from which the files originate and then split by userid.
Additionally to the file content, metadata for the logs are record to a database table.
These include:
- The id of the log (primary key)
- The owning app
- The owning user
- Timestamps for begin and end (as reported by the client) and upload (as seen by the server)
- Content encoding and filename suffix (as reported by the client)
- Completion status of the upload

### User Registration Service

The user registration provides two main API routes: Registering a new user and performing a login operation for an existing user.

The registration route always takes a user secret and can optionally take a username.
In both cases, the client is provided with a user id upon successful registration.
Depending on the application, the registration service also takes a set of application-specific user properties.
These are defined in the user registration service's database with their data type and whether they are optional or required.
Not providing required properties or providing undefined or incorrectly typed properties makes the registration fail.

The login route takes a set of user credentials in addition to the application credentials mentioned above and attempts to log the user in using those credentials.
Two kinds of user credentials are supported:
- userid and user secret: Here, the user secret is usually auto-generated on the client on registration and stored on the device to allow device-based authentication.
- username and user secret: Here, a user-specified username is used on registration and the user secret is usually a user-supplied password. This mode can be used for applications that require a password-based user registration anyways and allows coupling the SGL Analytics user registration to the general user account for the application.
