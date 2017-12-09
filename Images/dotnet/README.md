**Work in progress: Do not use**

# Image Tags

These images are tagged using the corresponding version from the Microsoft repo plus the image build date.

# Details

neonCLUSTER base image for Microsoft .NET Core Linux containers.  These images are based off of the corresponding versions at [microsoft/dotnet](https://hub.docker.com/r/microsoft/dotnet/).

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
