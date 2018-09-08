Ubuntu 16.04 image with recent package upgrades and a few handy packages.

# Image Tags

These base images are tagged with the source image build date.  The most recent production build will be tagged as `latest`.

From time-to-time you may see images tagged like `:BRANCH-*` where *BRANCH* identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

Note that any images that extend this one should launch the [tini](https://github.com/krallin/tini) init manager as the first process within the container so that Linux signals will be forwarded to child processes and so zombie processes will be reaped.  You'll need to specify a Docker entrypoint like:

&nbsp;&nbsp;&nbsp;&nbsp;`ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]`

# Additional Packages

This image includes updates to the official Ubuntu image and adds the following packages:

* [tini](https://github.com/krallin/tini) a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
* [wget](https://www.gnu.org/software/wget/) is a tool for downloading files, etc.
* [curl](https://curl.haxx.se/) is another file download tool.
* [jq](https://stedolan.github.io/jq/) a JSON parser
* [gosu](https://github.com/tianon/gosu) runs command as a specific user.
* `apt-transport-https` adds support for retrieving APT packages via HTTPS.
* `unzip` archive utilities
