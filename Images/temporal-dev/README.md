**WARNING: THIS IMAGE IS NOT INTENDED FOR PRODUCTION USE**

This image is intended to be run locally on a developer workstation or build/test machine for testing and development purposes.

## Image Tags

These images are tagged with the Temporal server version plus the image build date and the latest image will be tagged with `:latest`.

## Description

This image combines Temporal server and its backing Cassendra database and is intended for local Temporal unit and integration testing.

## Execute

You can start this container locally via:
```
docker run -d --name temporal-dev -p 7233-7239:7923-7239 -p 8088:8088 nkubeio/temporal-dev:latest
```

## Notes

**Exposed Ports**: 
* Temporal Ports: `7233-7239`,`8088`
* Cassandra Ports: `9042`
