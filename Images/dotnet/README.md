# Image Tags

These images are tagged using the corresponding version from the Microsoft repo plus the image build date.

# Description

neonHIVE base runtime images for Microsoft .NET Core Linux containers.  These images are based off of the corresponding basic **Alpine ASP.NET runtime** images at [microsoft/dotnet](https://hub.docker.com/r/microsoft/dotnet/).  Use the larger [nhive/aspnet](https://hub.docker.com/r/nhive/aspnet/) image if you need ASP.NET support.

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
