**Do not use: Work in progress**

# Supported Tags

* `1.0.0, 1.0, 1, latest`

# Description

The **neon-cluster-manager** service performs a few cluster maintenance functions:

* Updating the cluster definition persisted to Consul so it accurately describes the current cluster nodes and their labels.

* Monitoring the Vault seal status and optionally unsealing Vault automatically.

## Cluster Definition

NeonClusters persist a cluster defintion to Consul.  This is downloaded by the **neon-tool** and so that it can accurately make static container placement decisions for situations where Docker services are not appropriate.  The cluster definition includes the non-confidential properties when the cluster was initialized provisioned plus the current set of cluster nodes including their Docker labels.

This definition is serialized as JSON and then compressed via deflate before being persisted to Consul.  **neon-cluster-manager** also persists the MD5 hash of the definition JSON to Consul, making it possible to defer retrieving the definition only when it changes.  Nodes will be added or deleted and their labels modified relatively infrequently for many clusters, so this is a good optimization.

**neon-cluster-manager** persists the cluster definition and its hash to Consul at:
````
neon:
    cluster:
        definition.deflate  – (json/compressed) the current cluster definition
        definition.hash     - MD5 hash of the definition (base64)
````
&nbsp;
This container needs to query the Docker API on a manager node to list the swarm nodes.  Ideally, we would have deployed **neon-cluster-manager** as a Docker service and mounted the */var/run/docker.sock* into the container.  Unforunately, .NET Core doesn't currently provide a way to submit HTTP queries to Unix sockets at this time.  The alternative is to have the Docker daemon also listen on the `127.0.0.1` loopback address and submit requests there.  This works, but it requires the container to be able to bind to the host network, something Docker services can't do.

So we're left with running this as a container on each of the manager nodes.  The current implementation does not implement a leader/follower scheme, so this means that each **neon-cluster-manager** manager will query its host manager node every 30 seconds and persist any detected changes to Consul every 30 seconds.  The cluster definition and hash are written as a transaction to prevent consistency issues.

## Vault Unsealing

NeonCluster uses HashiCorp [Vault)(http://vaultproject.io) as a secure place to store cluster secrets like hosting environment credentials, certificate private keys and VPN certificate authority secrets.  Vault servers are deployed on manager nodes and use Consul as its backing store, encrypting the information stored there.

Vault is super secure by default, so secure that the keys required by Vault to decrypt its storage are not persisted anywhere in the cluster.  This means that after a Vault instance or its host manager node restarts, the instance is unable to decrypt its data.  This is called the **sealed** state.  Cluster operators will need to manually **unseal** Vault by providing keys returned when the Vault was first provisioned.  The **neon-tool** provides a command to accomplish this.

**neon-cluster-manager** periodically polls the Vault seal status on each manager and emits error logs when any instances are sealed so that downstream monitoring can issue an alert.proxy

Many clusters don't need this level of security and operators may wish that Vault could be be automatically unsealed after restarts.  **neon-cluster-manager** can be configured to accomplish this (in fact, NeonClusters are configured this way by default).  You may also use the **neon-cli** to enable or disable this for existing clusters using via:

&nbsp;&nbsp;&nbsp;&nbsp;`neon vault auto-unlock on|off`

# Environment Variables

* **LOG_LEVEL** (*optional*) Specifies the logging level: `FATAL`, `ERROR`, `WARN`, `INFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

* **VAULT_CREDENTIALS** (*optional*) Specifies the Vault unseal keys.  The keys are serialized as JSON and then base64 encoded as described below.

# Consul Settings

This container reads configuration settings from Consul:

&nbsp;&nbsp;&nbsp;&nbsp;`neon/services`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neon-cluster-manager:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`node_poll_seconds: 30`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`vault_poll_seconds: 30`

* **node_poll_seconds** (*double*) The number of seconds the service will wait between retrieving the current cluster node information from the manager hosting the service and then updating the definition in Consul if it has changed (defaults to 30 seconds).

* **vault_poll_seconds** (*double*) The number of seconds the service will wait between Vault seal status checks (defaults to 30 seconds).

You'll need to restart the containers to pick up any changes.

# Unseal Keys

Since we have to deploy this as a container, we're not able to pass the Vault unseal keys as a Docker secret (that only works for services).  Instead, we need to pass these as an environment variable.  The unseal keys are generated by Vault during cluster setup and persisted to the operator's workstation in the cluster login file.  The JSON fragment will look something like:

````
{
    "UnsealKeys": [
        "nNobx5u5q3JeQM6d/kGkJflUwbg7QSQyEnMGf9wgzdE="
    ],
    "KeyThreshold": 1
}
````
&nbsp;
Your keys need to be base64 encoded and then passed in the **VAULT_CREDENTIALS** environment variable to enable automatic unsealing.

# Deployment

**neon-consul-manager** will be deployed automatically by **neon-tool** during cluster setup.
