# SGL Analytics

This repository contains a system for recording and collecting game telemetry logs for serious games.
The collected logs are protected using end-to-end encryption.
To prevent injection of additional recipient public keys, signatures are used for enforcing that only properly signed public keys are accepted by the in-game client.
The logs are also stored separately form the user accounts in a backend which is split into microservices for additional isolation.

The system was originally developed for collecting in-game telemetry for serious games developed in the Serious Games Lab (SGL) at Trier University of Applied Sciences.

The repository consists the folowing primary components:
- A client library that is used by games to record gameplay data in the form of events and snapshots.
	After recording these data locally, they are uploaded to the backend through a background process.
	A simple example application using SGL Analytics is provided in the [SGL.Analytics.Client.Example directory](SGL.Analytics.Client.Example/)
- A backend consisting of the following main services:
	- User Registration: Manages user credentials and application-specific user data (end-to-end encrypted and clear-text key-value property sets are supported)
	- Logs Collector: Accepts the actual file uploads from the clients and catalogs the file by application and user, including relevant metadata, most notably the required key material
- An exporter client library that is used in tools for the receiving end of the data collection:
	- Handles:
		- Authentication with the backend
		- Downloading of data and associated key material
		- Decryption of data keys and data using the user's private key
	- Decrypted log files and user data are passed to sink objects
	- Tool provides sink object implementations with the processing logic

Furthermore, this project contains
- The [application registration tool](SGL.Analytics.Backend.AppRegistrationTool/index.md) that is used to make new applications / games known to the backend services
- Components needed to run the backend containerized in `docker-compose`
	- A [Postgres-based database container image](SGL.Analytics.Backend.DB/) that is automatically prepared with the needed databases and user accounts
	- An [Nginx-based container image for an API Gateway](SGL.Analytics.Backend.APIGW/) to distribute requests coming into a server to the two backend services, to simplify single-server deployments
	- The `docker-compose` definition files backend deployment

Note that the end-to-end encryption itself is implemented in the SGL.Utilities.Crypto package, provided by [SGL Utilities](https://github.com/TUAS-Serious-Games-Lab/SGL-Analytics),
as the encryption is also used in other Serious Games Lab components.
The actual cryptography primitives used are provided by [the Bouncy Castle project](http://www.bouncycastle.org/csharp/).

Moving this project to Github for public availability is currrently still work in progress.
For this reason, there may be references to locations on internal Gitlab repository locations that don't work on the public Github repository.
Continuous integration pipelines and other automations are also not yet implemented.
We work towards rectifying this to eventually make the repository cleanly available in both environments.
