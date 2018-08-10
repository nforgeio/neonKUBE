Base HAProxy (Alpine) image that initializes itself with a mounted configuration file.

# Image Tags

Supported images are tagged with the HAProxy version plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

The following image tags identify archived images which will not be deleted but are no longer maintained.

* `1.6.9`
* `1.6.10`
* `1.7.0`
* `1.7.1`
* `1.7.2`
* `1.7.8`

# Description

HAProxy (Alpine) image that initializes itself with a mounted configuration file.

# Configuration

To use this, all you need to do is copy or mount the HAProxy configuration file to:

&nbsp;&nbsp;&nbsp;&nbsp;**/usr/local/etc/haproxy/haproxy.cfg**

The container polls the configuration file for changes and cleanly restarts the proxy when any are detected.  Polling defaults to a 5 second interval but this can be controlled by passing the **CONFIG_INTERVAL** environment variable as the desired interval in seconds.  Pass 0 to disable polling.

Note: This container will load environment variables from `/etc/neon/env-host` if this has been mounted to the container.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
