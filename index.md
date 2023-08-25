# SGL Analytics

This project provides functionality to record and collect analytics raw data for Serious Games Labs projects.
It consists of two main parts:
- A client library that is used by games to record gameplay data in the form of events and snapshots.
	After recording these data locally, they are uploaded to the backend through a background process.
	For more information on using the client library, see the [API Documentation](https://serious-games-lab.pages.gitlab.rlp.net/sgl-analytics/api/SGL.Analytics.Client.SglAnalytics.html).
	A simple example application using SGL Analytics is provided in the [SGL.Analytics.Client.Example directory](https://gitlab.rlp.net/serious-games-lab/sgl-analytics/-/tree/main/SGL.Analytics.Client.Example)
- A backend consisting of the following main services:
	- User Registration: Manages user credentials and application-specific user data
	- Logs Collector: Accepts the actual file uploads from the clients and catalogs the file by application and user, including relevant metadata

Furthermore, this project contains
- The [application registration tool](SGL.Analytics.Backend.AppRegistrationTool/index.md) that is used to make new applications / games known to the backend services
- Components needed to run the backend containerized in `docker-compose`
	- A [Postgres-based database container image](https://gitlab.rlp.net/serious-games-lab/sgl-analytics/-/tree/main/SGL.Analytics.Backend.DB) that is automatically prepared with the needed databases and user credentials
	- An [Nginx-based container image for an API Gateway](https://gitlab.rlp.net/serious-games-lab/sgl-analytics/-/tree/main/SGL.Analytics.Backend.APIGW) to distribute requests coming into the server to the two backend services
	- The `docker-compose` definition files
