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
    --env POLL_INTERVAL=15s \
    --env VERIFY_INTERVAL=5m \
    --env LOG_LEVEL=INFO \
    --constraint node.role==manager \
    --mode global \
    --restart-delay 10s \
    neoncluster/neon-dns
````
