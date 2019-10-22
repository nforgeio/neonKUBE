# Image Tags

These images are tagged using the corresponding version from the Microsoft repo plus the image build date.

# Description

This repo includes the base runtime images for Microsoft .NET Core Linux containers.  Use the larger [nkubeio/aspnet](https://hub.docker.com/r/nkubeio/aspnet/) image if you need ASP.NET support.

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
