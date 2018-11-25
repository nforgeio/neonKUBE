# Image Tags

Supported images are tagged with the Varnish version plus the image build date.

# Description

This image integrates the high performance caching HTTP proxy (Varnish Cache)[http://varnish-cache.org] into the neonHIVE traffic manager infrastructure.  This is deployed automatically as required.  This is not suitable for any other purpose.  You can use the [varnish](https://hub.docker.com/r/nhive/varnish/) image to deploy custom caches as part of Docker stacks, etc.

# Environment Variables

This image is configured by the following environment variables:

* `CONFIG_KEY` (*required*) - Consul key holding the HAProxy ZIP archive configuration.

* `CONFIG_HASH_KEY` (*required*) - Consul key holding MD5 hash of the configuration used when polling for changes.

* `MEMORY_LIMIT` (*optional*) - Specifies the maximum RAM to be allocated to the cache.  This can simply be the number of bytes or you can append `K`, `M`, or `G` to specify kilobytes, megabytes, or gigabytes (defaults to `100M`).  Note that this service will allocate a minimum of 50MB RAM for the cache.

* `WARN_SECONDS` (*optional*) - seconds between logging warning while HAProxy is running with an out-of-date configuration.  This defaults to 300 (5 minutes).

* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

* `DEBUG` (*optional*) - set to `true` to enable debug mode.  In this mode, the service will not delete the proxy configuration and certificate after HAProxy loads them into memory so these can be examined for debugging purposes.  HAProxy will also be started in debug mode so that it will emit extensive activity information to standard output.

# Deployment

This image is deployed automatically by the **neon-proxy-manager** service when one or more traffic manager rules enable caching for the **public** and/or **private** traffic manager.  The **neon-proxy-public-cache** service will be deployed for the **public** traffic manager and **neon-proxy-private-cache** for the **private** load balancer.

**neon-proxy-manager** deploys the cache services with settings like:

```
docker service create \
    --name neon-proxy-public-cache \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --mount type=tmpfs,dst=/var/lib/varnish/_.vsm_mgt,tmpfs-size=90M,tmpfs-mode=755 \
    --env CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-conf \
    --env CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-hash \
    --env WARN_SECONDS=300 \
    --env MEMORY-LIMIT=100M \
    --env LOG_LEVEL=INFO \
    --env DEBUG=false \
    --secret neon-proxy-public-credentials \
    --constraint node.role==worker \
    --replicas 1 \
    --restart-delay 10s \
    --network neon-public \
    nhive/neon-proxy-cache

docker service create \
    --name neon-proxy-private-cache \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --mount type=tmpfs,dst=/var/lib/varnish/_.vsm_mgt,tmpfs-size=90M,tmpfs-mode=755 \
    --env CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-conf \
    --env CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-hash \
    --env WARN_SECONDS=300 \
    --env MEMORY-LIMIT=100M \
    --env LOG_LEVEL=INFO \
    --env DEBUG=false \
    --secret neon-proxy-private-credentials \
    --constraint node.role==worker \
    --replicas 1 \
    --restart-delay 10s \
    --network neon-private \
    nhive/neon-proxy-cache
```

## Important: Varnish Shared Memory Log

Varnish highly recommends [here](https://book.varnish-software.com/4.0/chapters/Tuning.html#the-varnish-shared-memory-log-vsl) that production deployment map the `/var/lib/varnish/_.vsm_mgt` directory to a **tmpfs** to avoid excessive I/O when writing logs.  Varnish requires **80MB** of space by default.  The example above sets this to **90MB** to provide a bit of a buffer.

Due to unavailable Docker features, this requires a slightly customized version of `varnishd` built by [nhive/varnishbuilder](https://hub.docker.com/r/nhive/varnish-builder/).
