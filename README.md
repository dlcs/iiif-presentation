# IIIF Presentation

Allows for the creation and management of IIIF Manifests and Collections.

## Local Development

There is a docker-compose file for running resources required for local development.

```bash
# create .env file (1 time only)
cp .env.dist .env

# run via docker-compose
docker compose -f docker-compose.local.yml up
```

### Database

migrations can be applied by setting the app setting `"RunMigrations": true` in the API and then inital data can be seeded from the [create_root script](/scripts/create_root.sql) in the scripts folder.

Migrations can be added with the command

```bash
dotnet ef migrations add "<migration name>" -p Repository -s API
```

if you would like to view the SQL the migration will produce, you can use the following command:

```bash
dotnet ef migrations script -i -o .\migrate.sql -p Repository -s API
```

### DLCS Named Query

As part of handling assets in canvas paintings, items are ingested via the DLCS.  In order to track items that have been ingested, a new global named query needs to be added to the DLCS to track manifests.  This can be done in 2 ways, as follows:

1. POST to the named query endpoint:

**NOTE:** This is required to be done as the admin customer using the DLCS API

```
POST {{baseUrl}}/customers/1/namedQueries

{
    "name": "manifest-query",
    "template": "manifest=p1",
    "global": true
}
```

2. Directly into the DLCS database

```sql
INSERT INTO "NamedQueries" ("Id", "Customer", "Name", "Global", "Template")
VALUES (gen_random_uuid(), 1, 'manifest-query', true, 'manifest=p1')
```

**NOTE:** the presentation API assumes the name of this named query is `manifest-query` by default, so if this is changed the presentation will need an updated setting to track.


### Architecture

The IIIF Presentation solution is made up of a series of C# projects, scripts and databases this section is a quick discussion of the code make-up and architecture for future reference

#### C# Projects

| name | description |
|---|---|
| Utils/Migrator | Used to update the database when there is a pending migration that needs to be applied.  The API can also do this when configured |
| API | Contains the Web API application that users interact with |
| AWS | Module that contains calls and helper functions that interact with AWS |
| BackgroundHandler | Contains anything that needs to occur after actions from third-party services have completed, such as interactions with Protagonist |
| Core | Low-level module that provides helper functions to all projects  |
| DLCS | Contains calling code for the DLCS to allow images to be ingested and retrieved |
| Models | POCO's used throughout the solution |
| Repository | Used primarily to provide access to the database context, as well as various helper functions and some data access classes |
| Services | Contains functions that are shared by running applications only, such as the API and BackgroundHandler |

The general hierarchy of dependencies from lowest to highest are as follows:

|Hierarchy|
|---|
| Core, Models |
| AWS, Repository, DLCS |
| Services |
| API, BackgroundHandler |
