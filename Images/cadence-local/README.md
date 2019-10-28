# Cadence-Local

**WARNING: THIS IMAGE IS NOT INTENDED FOR PRODUCTION USE**

This image is intended to be run locally on a developer workstation or build/test machine for testing and development purposes.

## Image Tags

These images are tagged with the Uber Cadence version version plus the image build date and the latest image will be tagged with `:latest`.

## Description

This image combines Uber Cadence and its backing Cassendra database and is intended for local Cadence unit and integration testing.

## Notes

**Exposed Ports**: 
* Cadence Ports: `7933`,`7934`,`7935`,`7939`
* Cassandra Ports: `9042`
