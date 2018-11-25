# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`.

# Configuration

This image implements the `neon-proxy-vault` service which is responsible for forwarding internal requests to the HashiCorp Vault instances running on the hive manager nodes.  This function could not be handled by the **neon-proxy-private** service because that service needs to call Vault to retrieve secrets such as TLS certificates and keys. 

This image derives from `neoncloud/haproxy` but deploys its own `haproxy.cfg` file.  It also ignores the `CONFIG_INTERVAL` environment variable, if passed.

The load balancer service listens internally on the standard Vault `port 8200` which should be published to the Docker ingress network on port `HiveHostPorts.ProxyVault` (`5004`) to make the service available to Docker hosts.

Note that this image will load environment variables from `/etc/neon/host-env` if this has been mounted to the container.

# Environment Variables

* `VAULT_ENDPOINTS` (*required*) - identifies the Vault manager hostnames and ports where Vault will be listening.  These must be passed a comma separated `NODE:VAULT-HOSTNAME:PORT`, like:

&nbsp;&nbsp;&nbsp;&nbsp;`VAULT_ENDPOINTS=manager-0:manager-0.neon-vault.HIVENAME.nhive.io:8200,manager-1:manager-1.neon-vault.HIVENAME.nhive.io:8200,...`

* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

**IMPORTANT:** 

Do not attempt to mount a HAProxy configuration file to the container at `/etc/haproxy/haproxy.cfg`.  Doing so will cause the container to fail if the file is read/only or else the mapped file will be overwritten.

# Deployment

This service is typically deployed using a command like:

```
docker service create \
    --name neon-proxy-vault \
    --detach=false \
    --mode global \
    --endpoint-mode vip \
    --network neon-private \
    --constraint node.role==manager \
    --publish 5004:8200 \
    --mount type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --env VAULT_ENDPOINTS=manager-0:manager-0.neon-vault.HIVENAME.nhive.io:8200 \
    --env LOG_LEVEL=INFO \
    --restart-delay 10s \
    nhive/neon-proxy-vault:jeff-tls-latest
```
