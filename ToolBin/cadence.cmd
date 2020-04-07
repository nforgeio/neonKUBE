@echo off
REM Executes the Cadence CLI as a Docker container, passing thru any command line arguments.

docker run --rm ubercadence/cli:master  %*
