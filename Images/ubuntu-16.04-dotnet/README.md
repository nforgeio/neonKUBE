Ubuntu 16.04 image with the .NET Core (does not include ASP.NET)

# Image Tags

These images are tagged with the Microsoft .NET Core runtime version plus the image build date.  The most recent build will be tagged as `latest`.

# Description

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packges

This image extends the latest [nkubeio/ubuntu-16.04](https://hub.docker.com/r/nkubeio/ubuntu-16.04/) by installing the .NET Core runtime (including ASP.NET).  The image tag identifies the .NET Core version installed.
