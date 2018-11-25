# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`

# Description

The `neon-dns-mon` service integrates with `neon-dns` and PowerDNS installed in a neonHIVE to provide dynamic DNS capabilities.

`neon-dns-mon` runs as a single replica, typically on one of the manager nodes.  It is responsible for monitoring the DNS entries located in Consul at `neon/dns/entries` checking these endpoints for health and then updating the hive hosts file at `neon/dns/answers/hosts.txt`.

`neon-dns` is deployed as a global service on all manager nodes.  Each of these instances monitor `neon/dns/answers/hosts.txt` for changes and updates the local PowerDNS hosts file and then signals the local `neon-dns-loader` service to have PowerDNS reload the hosts on the managers.  Once this happens, all hive nodes will see the change as entry TTLs expire because the managers act as the upstream nameservers for the hive.

# Environment Variables

* `NAMESERVERS` (*optional*) - specifies the upstream DNS nameserver IP addresses (comma separated) to be used to resolve hostnames.  This defaults to the Google nameservers [8.8.8.8,4.4.4.4] but should be set to ${NEON_UPSTREAM_DNS} which is configured correctly on every hive host.

* `PING_TIMEOUT` (*optional*) - specifies the maximum time to wait for an ICMP ping health check response.  This defaults to `1.5 seconds`

* `POLL_INTERVAL` (*optional*) - specifies the interval the service uses when monitoring DNS host entries for changes.  This defaults to `5 seconds`.

* `WARN_INTERVAL` (*optional*) - specifies the interval service uses to limit logged warnings.  This defaults to `5 minutes`.

* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Deployment

`neon-dns-mon` service will be deployed automatically as a single replica by `neon-cli` during hive setup using a command like:

````
docker service create \
    --name neon-dns-mon \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --env NAMESERVERS=${NEON_UPSTREAM_DNS} \
    --env PING_TIMEOUT=1.5s \
    --env LOG_LEVEL=INFO \
    --env POLL_INTERVAL=5s \
    --env WARN_INTERVAL=5m \
    --constraint node.role==manager \
    --replicas 1 \
    --restart-delay 10s \
    nhive/neon-dns-mon
````
&nbsp;
**NOTE:** This service may be deployed anywhere on the hive but we constrain it to manager nodes as a convention.
