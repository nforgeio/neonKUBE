# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional **-dirty** suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as **latest**.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This service dynamically generates HAProxy configurations from load balancer rules and certificates persisted to Consul and Vault for neonCLUSTER proxies based on the [neon-proxy](https://hub.docker.com/r/neoncluster/neon-proxy/) image.

neonCLUSTERs deploy two general purpose reverse HTTP/TCP proxy services:

* **neon-proxy-public** which implements the public load balancer and is responsible for routing external network traffic (e.g. from an Internet facing load balancer or router) to cluster services.

* **neon-proxy-private** which implements the private load balancer is used for internal routing for the scenarios the Docker overlay ingress network doesn't address out-of-the-box (e.g. load balancing and fail-over for groups of stateful containers that cannot be deployed as Docker swarm mode services).

These proxy services are based on the [neon-proxy](https://hub.docker.com/r/neoncluster/neon-proxy/) image which deploys [HAProxy](http://haproxy.org) that actually handles the routing, along with some scripts that can dynamically download the proxy configuration from HashiCorp Consul and TLS certificates from HashiCorp Vault.

The **neon-proxy-manager** image handles the generation and updating of the proxy service configuration in Consul based on proxy definitions and TLS certificates loaded into Consul by the **neon-cli**.

# Environment Variables

* **VAULT_CREDENTIALS** (*required*) - names the file within `/run/secrets/` that holds the Vault credentials the proxy manager will need to access TLS certificates.

* **LOG_LEVEL** (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Secrets

**neon-proxy-manager** needs to be able to read the TLS certificates stored in Vault and also be able to read/write Consul neonCLUSTER service keys for itself as well as **neon-proxy-public** and **neon-proxy-private**.  The credentials are serialized as JSON to the `/run/secrets/${VAULT_CREDENTIALS}` file using the Docker secrets feature.

Two types of credentials are currently supported: **vault-token** and **vault-approle**.

**token:**
&nbsp;&nbsp;&nbsp;&nbsp;`{`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"Type": "vault-token",`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"VaultToken": "65b74ffd-842c-fd43-1386-f7d7006e520a"`
&nbsp;&nbsp;&nbsp;&nbsp;`}`

**approle:**
&nbsp;&nbsp;&nbsp;&nbsp;`{`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"Type": "vault-approle",`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"VaultRoleId": "db02de05-fa39-4855-059b-67221c5c2f63",`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"VaultSecretId": "6a174c20-f6de-a53c-74d2-6018fcceff64"`
&nbsp;&nbsp;&nbsp;&nbsp;`}`

This service also requires Consul read/write access to `neon/service/neon-proxy-manager/*`, `neon/service/neon-proxy-public` and `neon/service/neon-proxy-private`.  neonCLUSTER does not currently enforce security on Consul, so there's no authentication necessary for this yet.

# Consul Settings

**neon-proxy-manager** retrieves its settings from Consul as well as the active route definitions for the **public** and **private** cluster proxies.  Consul also holds the generated HAProxy configurations that the **neon-proxy** service instances serve.

&nbsp;&nbsp;&nbsp;&nbsp;`neon/service:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neon-proxy-manager:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`poll-seconds: 120`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-warn-days: 30`

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`status:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`public: <LoadBalancer json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`private: <LoadBalancer json>`
        
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`proxies:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`public:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`conf: haproxy.zip`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`hash: <MD5 hash of conf+certs>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`private:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`conf: haproxy.zip`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`hash: <MD5 hash of conf+certs>`

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`conf:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`reload`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-update`

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`public:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`settings: <LoadBalancerSettings json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`rules:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name1: <LoadBalancerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name2: <LoadBalancerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`...`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`private:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`settings: <LoadBalancerSettings json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`rules:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name1: <LoadBalancerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name2: <LoadBalancerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`...`

* **poll-seconds** (*double*) - how often the proxy manager should scan TLS certificates persisted in Vault for expiration checks and updates and also poll the individual proxy definitions for changes.

* **cert-warn-days** (*double*) - number of days in advance to begin warning of certificate expirations.

* **proxies/.../conf** - public or private proxy's generated HAProxy configuration as a ZIP archive.

* **proxies/.../hash** - MD5 hash of the public or private load balancer's conf archive combined with the hash of all of the referenced certificates.  This is used by **neon-proxy** instances to detect when the proxy configuration has changed.

* **status/...** (*json*) - proxy rule status at the time the **neon-proxy-manager** last processed cluster rules for the named load balancer.

* **conf** - root key for proxy settings that need to be monitored for changes.

* **reload** - touched when the neon proxy NAME reload command is executed.  **neon-proxy-manager** monitors this and republishes immediately on a change.

* **cert-update** - touched by the **neon-cli** whenever certificates are modified.  **neon-proxy-manager** monitors this and republishes immediately on a change.

* **settings** - global (per proxy) settings for a load balancer formatted as JSON.

* **rules** - named load balancer rules formatted as JSON.

# Vault

**neon-proxy-manager** reads TLS certificates from Vault and includes these in the generated HASProxy configurations.  Vertificates are stored by name as JSON.  Here's where this is located in Vault.

&nbsp;&nbsp;&nbsp;&nbsp;`neon-secret:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-1: <cert json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-2: <cert json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`...`

# Deployment

**neon-proxy-manager** is typically deployed only to manager nodes.  Multiple instances may be run safely because they will coordinate their activities using a Consul lock, but the best practice is to deploy this as a Docker swarm mode service with one replica constrained to manager nodes with **mode=global**.  This relies on Docker to ensure that only one instance is running.

**neon-cli** deploys **neon-proxy-manager** when the cluster is provisioned using this Docker command:

````
docker service create \
    --name neon-proxy-manager \
    --detach=false \
    --mount type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --env VAULT_CREDENTIALS=neon-proxy-manager-credentials \
    --env LOG_LEVEL=INFO \
    --secret neon-proxy-manager-credentials \
    --constraint node.role==manager \
    --replicas 1 \
    --restart-delay 10s \
    --log-driver fluentd \
    neoncluster/neon-proxy-manager
````
&nbsp;
