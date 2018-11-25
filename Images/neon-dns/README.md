# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`

# Description

The `neon-dns` service integrates with `neon-dns-mon` and PowerDNS installed in a neonHIVE to provide dynamic DNS capabilities.

`neon-dns-mon` runs as a single replica, typically on one of the manager nodes.  It is responsible for monitoring the DNS entries located in Consul at `neon/dns/entries` checking these endpoints for health and then updating the hive hosts file at `neon/dns/answers/hosts.txt`

`neon-dns` is deployed as a global service on all manager nodes.  Each of these instances monitor `neon/dns/answers/hosts.txt` for changes and updates the local PowerDNS hosts file and then signals the local `neon-dns-loader` service to have PowerDNS reload the hosts on the managers.  Once this happens, all hive nodes will see the change as entry TTLs expire because the managers act as the upstream nameservers for the hive.

# Environment Variables

* `POLL_INTERVAL` (*optional*) - specifies the interval used when polling Consul for DNS changes.  This defaults to `5 seconds`

* `VERIFY_INTERVAL` (*optional*) - specifies the interval used verify that the manager has the correct hosts even when no changes were detected from Consul.  This defaults to `5 minutes`.

* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Deployment

`neon-dns` service will be deployed automatically by `neon-cli` during hive setup using a command like:

````
docker service create \
    --name neon-dns \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --mount type=bind,src=/etc/powerdns/hosts,dst=/etc/powerdns/hosts \
    --mount type=bind,src=/dev/shm/neon-dns,dst=/neon-dns \
    --env POLL_INTERVAL=5s \
    --env VERIFY_INTERVAL=5m \
    --env LOG_LEVEL=INFO \
    --constraint node.role==manager \
    --mode global \
    --restart-delay 10s \
    nhive/neon-dns
````
&nbsp;
# Remarks

The three file system mounts are required for the hive DNS to function properly.

* `/etc/neon/host-env` - is a script that the service uses to load node specific environment variables.

* `/etc/powerdns/hosts` - is the local PowerDNS Recursor hosts files where dynamic DNS entries will be written.

* `/dev/shm/neon-dns` - is a folder on the RAM drive where the `neon-dns` service creates the `reload` file to signal that PowerDNS Recursor needs to reload the hosts file.

`neon-dns` works in conjunction with a local systemd service called `neon-dns-loader`.  This service is a simple script that watches for the existence of a `/dev/shm/neon-dns\reload` file to signal PowerDNS Recursor to reload the hosts file.  `neon-dns-loader` is configured automatically during neonHIVE setup.
