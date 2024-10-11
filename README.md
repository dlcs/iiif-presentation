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