# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional **-dirty** suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as **latest**.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

The **neon-dns-mon** service integrates with **neon-dns** and PowerDNS installed in a neonCLUSTER to provide dynamic DNS capabilities.

# Environment Variables

* **NAMESERVERS** (*required*) - specifies the upstream DNS nameserver IP addresses (comma separated) to be used to resolve hostnames.  This is required and should be set to ${NEON_UPSTREAM_DNS} which is configured correctly on every cluster host.

* **POLL_INTERVAL** (*optional*) - specifies the interval the service uses when generating DNS host entries.  This defaults to **15 seconds**.

* **WARN_INTERVAL** (*optional*) - specifies the interval service uses to limit logged warnings.  This defaults to **5 minutes**.

* **LOG_LEVEL** (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Deployment

**neon-dns-mon** service will be deployed automatically as a single replica by **neon-cli** during cluster setup using a command like:

````
docker service create \
    --name neon-dns-mon \
    --detach=false \
    --env NAMESERVERS=${NEON_UPSTREAM_DNS} \
    --env LOG_LEVEL=INFO \
    --env POLL_INTERVAL=15s \
    --env WARN_INTERVAL=5m \
    --constraint node.role==manager \
    --replicas 1 \
    --restart-delay 10s \
    neoncluster/neon-dns-mon
````
&nbsp;
**NOTE:** This service may be deployed anywhere on the cluster but we constrain it to manager nodes as a convention.
