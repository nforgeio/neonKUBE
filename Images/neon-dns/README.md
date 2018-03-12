# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional **-dirty** suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as **latest**.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

The **neon-dns** service integrates with PowerDNS installed in a neonCLUSTER to provide dynamic DNS capabilities.

# Environment Variables

* **POLL_INTERVAL** (*optional*) - specifies the interval used when polling Consul for DNS changes.  This defaults to **15 seconds**.

* **VERIFY_INTERVAL** (*optional*) - specifies the interval used verify that the manager has the correct hosts even when no changes were detected from Consul.  This defaults to **5 minutes**.

* **LOG_LEVEL** (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Deployment

**neon-dns** service will be deployed automatically by **neon-cli** during cluster setup using a command like:

````
docker service create \
    --name neon-dns \
    --detach=false \
    --mount type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true \
    --mount type=bind,src=/etc/powerdns/hosts,dst=/etc/powerdns/hosts \
    --mount type=bind,src=/dev/shm/neon-dns,dst=/neon-dns \
    --env POLL_INTERVAL=15s \
    --env VERIFY_INTERVAL=5m \
    --env LOG_LEVEL=INFO \
    --constraint node.role==manager \
    --mode global \
    --restart-delay 10s \
    neoncluster/neon-dns
````
&nbsp;
# Remarks

The three file system mounts are required for the cluster DNS to function properly.

* `/etc/neoncluster/env-host` - is a script that the service uses to load node specific environment variables.

* `/etc/powerdns/hosts` - is the local PowerDNS Recursor hosts files where dynamic DNS entries will be written.

* `/dev/shm/neon-dns` - is a folder on the RAM drive where the **neon-dns** service creates the `reload` file to signal that PowerDNS Recursor needs to reload the hosts file.

**neon-dns** works in conjunction with a local systemd service called **neon-dns-loader**.  This service is a simple script that watches for the existence of a `/dev/shm/neon-dns\reload` file to signal PowerDNS Recursor to reload the hosts file.  *neon-dns-loader** is configured automatically during neonCLUSTER setup.
