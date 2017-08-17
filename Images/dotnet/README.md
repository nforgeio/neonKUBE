**Work in progress: Do not use**

neonCLUSTER base image for Microsoft .NET Core Linux containers.

# Supported Tags

* `1.0.3-runtime`
* `1.0.4-runtime, 1.0-runtime`
* `1.1.0-runtime`
* `1.1.1-runtime`
* `1.1.2-runtime, 1.1-runtime, 1-runtime`
* `2.0.0-runtime, 2.2-runtime, 2-runtime`

# Details

These images are based off of the corresponding versions at [microsoft/dotnet](https://hub.docker.com/r/microsoft/dotnet/).  These images are based on Debian.

Note that **latest** refers to the the latest runtime image.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
