# Image Tags

Ubuntu images are tagged with the Ubuntu version.

# Description

Enhances the official [Alpine](https://hub.docker.com/_/ubuntu/) image to include some additional packages.

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
* [wget](https://www.gnu.org/software/wget/) is a network tool for downloading files, etc.
