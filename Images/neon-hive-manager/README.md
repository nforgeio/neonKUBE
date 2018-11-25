# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`

# Description

The `neon-hive-manager` service performs a few hive maintenance functions:

* Updating the hive definition persisted to Consul so it accurately describes the current hive nodes and their labels.
* Monitoring the Vault seal status and optionally unsealing Vault automatically.
* Perdically broadcasting `ProxyRegenerateMessage` messages to `neon-proxy-manager` as a fail-safe to ensure that proxy related definitions are still valid (e.g. TLS certificates haven't expired) and that proxy configurations will eventually converge in the face of notification failures.

# Hive Definition

neonHIVEs persist a hive defintion to Consul.  This is downloaded by the `neon-cli` and so that it can accurately make container placement decisions for situations where Docker services are not appropriate.  The hive definition includes the non-confidential properties when the hive was initialized provisioned plus the current set of hive nodes including their Docker labels.

This definition is serialized as JSON and then compressed via deflate before being persisted to Consul.  `neon-hive-manager` also persists the MD5 hash of the definition JSON to Consul, making it possible to defer retrieving the definition only when it changes.  Nodes will be added or deleted and their labels modified relatively infrequently for many hives, so this is a good optimization.

`neon-hive-manager` persists the hive definition and its hash to Consul at:
````
neon:
    hive:
        definition-deflated – (json/compressed) the current hive definition
        definition-hash     - MD5 hash of the definition (base64)
````
&nbsp;
`neon-hive-manager` service instances need to be deployed only on hive manager nodes so it can access the local */var/run/docker.sock* Unix domain socket to query the Swarm status.  The service will be configured to run only one instance at a time although it is safe to run more than one.

# Vault Unsealing

neonHIVE uses HashiCorp [Vault)(http://vaultproject.io) as a secure place to store hive secrets like hosting environment credentials, certificate private keys and VPN certificate authority secrets.  Vault servers are deployed on manager nodes and use Consul as its backing store, encrypting the information stored there.

Vault is super secure by default, so secure that the keys required by Vault to decrypt its storage are not persisted anywhere in the hive.  This means that after a Vault instance or its host manager node restarts, the instance is unable to decrypt its data.  This is called the *sealed* state.  Hive operators will need to manually *unseal* Vault by providing keys returned when the Vault was first provisioned.  The `neon-cli` provides a command to accomplish this via the `neon-cli`:

&nbsp;&nbsp;&nbsp;&nbsp;`neon vault auto-unlock on|off`

Many (perhaps most) hives don't need this level of security and operators may wish that Vault could be be automatically unsealed after restarts.  `neon-hive-manager` can be configured to accomplish this by persisting the Vault unseal keys as a Docker secret named `neon-hive-manager-vaultkeys`  neonHIVEs are configured to do this by default.

# Environment Variables

* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Secrets

* `neon-ssh-credentials` - (*Required*) The service requires the SSH credentials to be mapped into the service as `neon-ssh-credentials`.  These credentials are formatted as `username/password`.

* `neon-hive-manager-vaultkeys` - (*Optional*) Pass this to enable automatic Vault unsealing.  This can be obtained from the root hive credentials and is a JSON object that will looks something like:
````
{
    "UnsealKeys": [
        "nNobx5u5q3JeQM6d/kGkJflUwbg7QSQyEnMGf9wgzdE="
    ],
    "KeyThreshold": 1
}
````
&nbsp;

# Consul Settings

This service reads configuration settings from Consul:

&nbsp;&nbsp;&nbsp;&nbsp;`neon/service`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neon-hive-manager:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`node_poll_seconds: 30`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`vault_poll_seconds: 30`

* `node_poll_seconds` (*double*) - seconds the service will wait between retrieving the current hive node information from the manager hosting the service and then updating the definition in Consul if it has changed (defaults to 30 seconds).

* `vault_poll_seconds` (*double*) - seconds the service will wait between Vault seal status checks (defaults to 30 seconds).

You'll need to restart the containers to pick up any changes.

# Deployment

`neon-hive-manager` service will be deployed automatically by `neon-cli` during hive setup using a command like:

````
docker service create \
    --name neon-hive-manager \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --mount type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock \
    --env LOG_LEVEL=INFO \
    --secret=neon-hive-manager-vaultkeys \
    --secret=neon-ssh-credentials \
    --secret neon-hivemq-neon \
    --constraint node.role==manager \
    --replicas 1 \
    --restart-delay 10s \
    nhive/neon-hive-manager
````
