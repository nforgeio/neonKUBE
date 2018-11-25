# Image Tags

Supported images are tagged with the Varnish version plus the image build date.

# Description

This image deploys a high performing caching HTTP proxy based on [Varnish Cache](http://varnish-cache.org).  This can be placed in front of websites to improve webpage loading performance and reduce the load from the backends.  This image is also extended by [neon-proxy-cache](https://hub.docker.com/r/nhive/neon-proxy-cache/) that integrates HTTP caching into the neonHIVE traffic manager infrastructure.

# Environment Variables

This image is configured by the following environment variables:

* `BACKEND_SERVER` (*required*) - Specifies the FQDN or IP address of the website being proxied.  This will typically name a Docker service running on the same network as the proxy.

* `BACKEND_PORT` (*optional*) - Specifies the backend port (defaults to `80`).

* `MEMORY_LIMIT` (*optional*) - Specifies the maximum RAM to be allocated to the cache.  This can simply be the number of bytes or you can append `K`, `M`, or `G` to specify kilobytes, megabytes, or gigabytes (defaults to `100M`).

# Deployment

This image is easy to deploy.  The typical use is to deploy as part of a Docker stack to proxy a website.

Here are the main considerations:

* `varnish` must be connected to the same network as the target website.
* `BACKEND_SERVER` must be set to the name of the Docker service hosting the website.

Here's a simple stack that deploys a Varnish cache listening on the port 8080 on the Docker ingress/mesh network (`mode: host`) and forwards traffic to a backend website while maintaining a 200M cache in RAM.

```
version: "3.2"
services:
  cache:
    image: nhive/varnish
    ports:
      - published: 8080
        target: 80
        protocol: tcp
        mode: host
    environment:
      - "BACKEND_SERVER=web"
      - "BACKEND_PORT=80"
      - "MEMORY_LIMIT=200M"
    volumes:
      - type tmpfs
        target: /var/lib/varnish/_.vsm_mgt
        tmpfs:
          size: 90M
          mode: 755
  web:
    image: nhive/node
```\

## Important: Varnish Shared Memory Log

Varnish highly recommends [here](https://book.varnish-software.com/4.0/chapters/Tuning.html#the-varnish-shared-memory-log-vsl) that production deployment map the `/var/lib/varnish/_.vsm_mgt` directory to a **tmpfs** to avoid excessive I/O when writing logs.  Varnish requires **80MB** of space by default.  The example above sets this to **90MB** to provide a bit of a buffer.

Due to unavailable Docker features, this requires a slightly customized version of `varnishd` built by [nhive/varnishbuilder](https://hub.docker.com/r/nhive/varnish-builder/).
