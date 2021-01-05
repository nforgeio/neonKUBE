**WARNING: THIS IMAGE IS NOT INTENDED FOR PRODUCTION USE**

This image is intended to be run locally on a developer workstation or build/test machine for testing and development purposes.

## Image Tags

These images are tagged with the Uber Cadence server version plus the image build date and the latest image will be tagged with `:latest`.

## Description

This image combines Uber Cadence and its backing Cassendra database and is intended for local Cadence unit and integration testing.

## Execute

You can start this container locally via:
```
docker run -d --name cadence-dev -p 7933-7939:7933-7939 -p 8088:8088 ghcr.io/neonrelease/cadence-dev:latest
```

## Notes

**Exposed Ports**: 
* Cadence Ports: `7933-7939`,`8088`
* Cassandra Ports: `9042`
