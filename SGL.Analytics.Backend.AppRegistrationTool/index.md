# Application Registration Tool

This tool executable subproject is used to manage application registrations in the backend services, manually or under automation.
The source code for the tool can be found [here](https://gitlab.rlp.net/serious-games-lab/sgl-analytics/-/tree/main/SGL.Analytics.Backend.AppRegistrationTool).

## Push operation

The main operation is the `push` operation, invoked by calling `dotnet SGL.Analytics.Backend.AppRegistrationTool push` with a directory as the parameter.
Additional `appsettings.json`-style config files (e.g. for database secrets) can be passed using `--config` arguments.
In this mode, the tool enumerates all JSON files in a given directory and registers the applications described in these files in databases of both backend services.
Each of the files is expected to contain a JSON version of a `SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties` object.
An example for such a file is given below:

```json
{
  "Name": "ExampleApp",
  "ApiToken": "<some token string used for identifying the app, can be generated using the generate-api-token verb of the tool>",
  "UserProperties": [
    {
      "Name": "Foo",
      "Type": "Integer",
      "Required": false
    },
    {
      "Name": "Bar",
      "Type": "String",
      "Required": false
    },
    {
      "Name": "Obj",
      "Type": "Json",
      "Required": false
    }
  ]
}
```

## API Token Generation

The tool also provides a mode that simply generates an API token, that can be used when writing a new app description JSON files.
This mode is activated by calling `dotnet SGL.Analytics.Backend.AppRegistrationTool push` and doesn't require a database connection or similar dependencies.

## Automation Usage

The tool is (in containerized form) also used during `docker-compose up` of the backend to ensure registration of all apps that have their definition present in an appropriate bind-mount volume.
This allows for easy application registration by just adding the app definition to the bound directory and triggering a `docker-compose up`.
Populating this directory can be done manually or by a continuous deployment job.
