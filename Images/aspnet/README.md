# Image Tags

These images are tagged using the corresponding version from the Microsoft repo plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

neonHIVE base runtime images for Microsoft .NET Core Linux containers.  These images are based off of the corresponding basic **Alpine runtime** images at [microsoft/dotnet](https://hub.docker.com/r/microsoft/dotnet/).  Use the smaller [nhive/dotnet](https://hub.docker.com/r/nhive/dotnet/) image if you don't need ASP.NET support.

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
