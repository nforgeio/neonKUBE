# Image Tags

Images are tagged with the Git branch, image build date, and Git commit.  The most recent production build will be tagged as **latest**.

# Description

The **neon-cluster-manager** service performs a few cluster maintenance functions:

* Updating the cluster definition persisted to Consul so it accurately describes the current cluster nodes and their labels.

* Monitoring the Vault seal status and optionally unsealing Vault automatically.

# Cluster Definition

neonCLUSTERs persist a cluster defintion to Consul.  This is downloaded by the **neon-cli** and so that it can accurately make container placement decisions for situations where Docker services are not appropriate.  The cluster definition includes the non-confidential properties when the cluster was initialized provisioned plus the current set of cluster nodes including their Docker labels.

This definition is serialized as JSON and then compressed via deflate before being persisted to Consul.  **neon-cluster-manager** also persists the MD5 hash of the definition JSON to Consul, making it possible to defer retrieving the definition only when it changes.  Nodes will be added or deleted and their labels modified relatively infrequently for many clusters, so this is a good optimization.

**neon-cluster-manager** persists the cluster definition and its hash to Consul at:
````
neon:
    cluster:
        definition.deflate  – (json/compressed) the current cluster definition
        definition.hash     - MD5 hash of the definition (base64)
````
&nbsp;
**neon-cluster-manager** service instances need to be deployed only on cluster manager nodes so it can access the local */var/run/docker.sock* Unix domain socket to query the Swarm status.  The service will be configured to run only one instance at a time although it is safe to run more than one.

# Vault Unsealing

neonCLUSTER uses HashiCorp [Vault)(http://vaultproject.io) as a secure place to store cluster secrets like hosting environment credentials, certificate private keys and VPN certificate authority secrets.  Vault servers are deployed on manager nodes and use Consul as its backing store, encrypting the information stored there.

Vault is super secure by default, so secure that the keys required by Vault to decrypt its storage are not persisted anywhere in the cluster.  This means that after a Vault instance or its host manager node restarts, the instance is unable to decrypt its data.  This is called the **sealed** state.  Cluster operators will need to manually **unseal** Vault by providing keys returned when the Vault was first provisioned.  The **neon-cli** provides a command to accomplish this via the **neon-cli**:

&nbsp;&nbsp;&nbsp;&nbsp;`neon vault auto-unlock on|off`

Many (perhaps most) clusters don't need this level of security and operators may wish that Vault could be be automatically unsealed after restarts.  **neon-cluster-manager** can be configured to accomplish this by persisting the Vault unseal keys as a Docker secret named **neon-cluster-manager-vaultkeys**.  neonCLUSTERs are configured to do this by default.

# Environment Variables

* **LOG_LEVEL** (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Consul Settings

This service reads configuration settings from Consul:

&nbsp;&nbsp;&nbsp;&nbsp;`neon/services`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neon-cluster-manager:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`node_poll_seconds: 30`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`vault_poll_seconds: 30`

* **node_poll_seconds** (*double*) - seconds the service will wait between retrieving the current cluster node information from the manager hosting the service and then updating the definition in Consul if it has changed (defaults to 30 seconds).

* **vault_poll_seconds** (*double*) - seconds the service will wait between Vault seal status checks (defaults to 30 seconds).

You'll need to restart the containers to pick up any changes.

# Secrets

To enable automatic Vault unsealing, you'll need to persist the Vault unseal keys as the Docker secret named **neon-cluster-manager-vaultkeys**.  This can be obtained from the root cluster credentials and is a JSON object that will looks something like:

````
{
    "UnsealKeys": [
        "nNobx5u5q3JeQM6d/kGkJflUwbg7QSQyEnMGf9wgzdE="
    ],
    "KeyThreshold": 1
}
````
&nbsp;

# Deployment

**neon-consul-manager** service will be deployed automatically by **neon-cli** during cluster setup using a command like:

````
docker service create \
    --name neon-cluster-manager \
    --mount type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --mount type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock \
    --env LOG_LEVEL=INFO \
    --secret=neon-cluster-manager-vaultkeys \
    --constraint node.role==manager \
    --replicas 1 \
    --restart-delay 10s \
    neoncluster/neon-cluster-manager
````
