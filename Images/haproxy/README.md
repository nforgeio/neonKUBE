Base HAProxy (Alpine) image that initializes itself with a mounted configuration file.

# Image Tags

Supported images are tagged with the HAProxy version plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

HAProxy (Alpine) image that initializes itself with a mounted configuration file.

# Configuration

To use this, all you need to do is copy or mount the HAProxy configuration file to:

&nbsp;&nbsp;&nbsp;&nbsp;/etc/haproxy/haproxy.cfg

The container polls the configuration file for changes and cleanly restarts the proxy when any are detected.  Polling defaults to a 5 second interval but this can be controlled by passing the **CONFIG_INTERVAL** environment variable as the desired interval in seconds.  Pass 0 to disable polling.
