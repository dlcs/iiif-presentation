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
