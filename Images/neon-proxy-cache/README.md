# Image Tags

Supported images are tagged with the Varnish version plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

The following image tags identify archived images which will not be deleted but are no longer maintained.

* `5.2.1-rc0`

# Description

This image can be config

The **neon-proxy-** image is deployed as the **neon-proxy-cache** service managed by **neon-proxy-manager** along with **neon-proxy-public**, **neon-proxy-private**, and **neon-proxy-api**.  

# Configuration

To use this, all you need to do is copy or mount the HAProxy configuration file to:

&nbsp;&nbsp;&nbsp;&nbsp;**/usr/local/etc/haproxy/haproxy.cfg**

The container polls the configuration file for changes and cleanly restarts the proxy when any are detected.  Polling defaults to a 5 second interval but this can be controlled by passing the **CONFIG_INTERVAL** environment variable as the desired interval in seconds.  Pass 0 to disable polling.

Note: This container will load environment variables from `/etc/neon/env-host` and/or `/etc/neon/env-container` if either of these files have been mounted to the container.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
