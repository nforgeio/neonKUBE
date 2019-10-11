# Image Tags

These images are tagged using the corresponding version from the Microsoft repo plus the image build date.

# Description

The repo includes the base runtime images for Microsoft ASP.NET Core Linux containers.  Use the smaller [nkubeio/dotnet](https://hub.docker.com/r/nkubeio/dotnet/) image if you don't need ASP.NET support.

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
