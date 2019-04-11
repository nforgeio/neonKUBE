**WARNING: THIS IMAGE IS NOT INTENDED FOR PRODUCTION USE**

# Image Tags

CADENCE-TEST images are tagged with the Uber Cadence version version plus the image build date and the latest image will be tagged with `:latest`.

# Description

This image combines Uber Cadence and its backing Cassendra database and is intended for local Cadence unit and integration testing.

# Notes

**Host**: By default the host is set to the Docker Host (usually `172.xx.x.x`).  This configuration can be found in `entrypoint.sh`

**Exposed Ports**: 
* Cadence Ports: `7933`,`7934`,`7935`,`7939`
* Cassandra Ports: `9042`



