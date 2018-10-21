# Image Tags

Supported images are tagged with the Varnish version plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where *BRANCH* identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This image integrates the high performance caching HTTP proxy (Varnish Cache)[http://varnish-cache.org] into the neonHIVE load balancer infrastructure.  This is deployed automatically as required.  This is not suitable for any other purpose.  You can use the [varnish](https://hub.docker.com/r/nhive/varnish/) image to deploy custom caches as part of Docker stacks, etc.

# Environment Variables

This image is configured by the following environment variables:

* `BACKEND_SERVER` (*required*) - Specifies the FQDN or IP address of the website being proxied.  This will typically name a Docker service running on the same network as the proxy.

* `BACKEND_PORT` (*optional*) - Specifies the backend port (defaults to `80`).

* `MEMORY_LIMIT` (*optional*) - Specifies the maximum RAM to be allocated to the cache.  This can simply be the number of bytes or you can append `K`, `M`, or `G` to specify kilobytes, megabytes, or gigabytes (defaults to `100M`).

# Deployment

This image is deployed automatically by the **neon-proxy-manager** service when one or more load balancer rules enable caching for the **public** and/or **private** load balancers.  The **neon-proxy-public-cache** service will be deployed for the **public** load balancer and **neon-proxy-private-cache** for the **private** load balancer.
