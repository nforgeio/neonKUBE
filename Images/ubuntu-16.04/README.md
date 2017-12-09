**DO NOT USE: Work in progress**

Ubuntu 16.04 image with recent package upgrades and a few handy packages.

# Image Tags

These images are tagged with the source branch, image build date, and the Git commit.  The most recent production build will be tagged as **latest**.

# Description

This image includes updates to the official Ubuntu image and adds the following packages:

* [tini](https://github.com/krallin/tini) a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
* [wget](https://www.gnu.org/software/wget/) is a tool for downloading files, etc.
* [curl](https://curl.haxx.se/) is another file download tool.
* [jq](https://stedolan.github.io/jq/) a JSON parser
* [gosu](https://github.com/tianon/gosu) runs command as a specific user.
* **apt-transport-https** adds support for retrieving APT packages via HTTPS.
* **unzip** archive utilities
