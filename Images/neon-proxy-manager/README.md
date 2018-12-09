# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`.

# Description

This service dynamically generates HAProxy configurations from traffic manager rules and certificates persisted to Consul and Vault for neonHIVE proxies based on the [neon-proxy-cache](https://hub.docker.com/r/nhive/neon-proxy-cache/) image.

neonHIVEs deploy four related reverse HTTP/TCP proxy services:

* `neon-proxy-public` which implements the public traffic manager and is responsible for routing external network traffic (e.g. from an Internet facing traffic manager or router) to hive services.

* `neon-proxy-private` which implements the private traffic manager is used for internal routing for the scenarios the Docker overlay ingress network doesn't address out-of-the-box (e.g. traffic manager and fail-over for groups of stateful containers that cannot be deployed as Docker swarm mode services).

* `neon-proxy-cache-public` which can provide caching services for the public proxy.

* `neon-proxy-cache-private` which can provide caching services for the private proxy.

The proxy services are based on the [neon-proxy](https://hub.docker.com/r/nhive/neon-proxy/) image which deploys [HAProxy](http://haproxy.org) that actually handles the routing, along with some scripts that can dynamically download the proxy configuration from HashiCorp Consul and TLS certificates from HashiCorp Vault.  The caching services are based on the [neon-proxy-cache](https://hub.docker.com/r/nhive/neon-proxy-cache/) which handles response caching.

The `neon-proxy-manager` image handles the generation and updating of the proxy service configurations in Consul based on proxy definitions and TLS certificates loaded into Consul by the `neon-cli`.  These configurations are consumed by the HAProxy `neon-proxy-public` and `neon-proxy-private`, `neon-proxy-cache-public` and `neon-proxy-cache-private` services.

`neon-proxy-manager` automatically manages the lifecycle of the `neon-proxy-cache-oublic` and `neon-proxy-cache-private` services by starting them when one or more corresponding traffic manager rules enable caching and stopping then when no rules enable caching.  This means that `neon-proxy-manager` may only be deployed to hive managers and that the Docker Unix domain socket must be mapped in, so the `neon-proxy-manager` can control the caching services.

**HiveMQ** broadcast channels are used by `neon-proxy-manager` to notify the proxy and service instances of configuration changes.  A change notification message is published to the **proxy-public-update** channel when the public proxy configuration has changed or to **proxy-private-update** for private changes.  The `neon-proxy-public` service listens to the **proxy-public-update** channel and `neon-proxy-private` to the **neon-private-update** channel.

# Environment Variables

* `VAULT_CREDENTIALS` (*required*) - names the file within `/run/secrets/` that holds the Vault credentials the proxy manager will need to access TLS certificates.

* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Secrets

`neon-proxy-manager` needs to be able to read the TLS certificates stored in Vault and also be able to read/write Consul neonHIVE service keys for itself as well as `neon-proxy-public` and `neon-proxy-private`.  The credentials are serialized as JSON to the `/run/secrets/${VAULT_CREDENTIALS}` file using the Docker secrets feature.

Two types of credentials are currently supported: `vault-token` and `vault-approle`.

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

This service also requires Consul read/write access to `neon/service/neon-proxy-manager/*`, `neon/service/neon-proxy-public` and `neon/service/neon-proxy-private`.  neonHIVE does not currently enforce security on Consul, so there's no authentication necessary for this yet.

# Consul Settings

`neon-proxy-manager` retrieves its settings from Consul as well as the active route definitions for the `public` and `private` hive proxies.  Consul also holds the generated HAProxy configurations that the `neon-proxy` service instances serve.

&nbsp;&nbsp;&nbsp;&nbsp;`neon/service:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neon-proxy-manager:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`poll-seconds: 120`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-warn-days: 30`

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`status:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`public: <TrafficManager json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`private: <TrafficManager json>`
        
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`proxies:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`public:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cache-conf: varnish.vcl`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cache-hash: <MD5 hash of cache-conf + certs>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`proxy-conf: haproxy.zip`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`proxy-hash: <MD5 hash of proxy-conf + certs>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`private:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cache-conf: varnish.vcl`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cache-hash: <MD5 hash of cache-conf + certs>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`proxy-conf: haproxy.zip`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`proxy-hash: <MD5 hash of proxy-conf + certs>`

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`conf:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`reload`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-update`

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`public:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`settings: <TrafficManagerSettings json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`rules:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name1: <TrafficManagerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name2: <TrafficManagerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`...`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`private:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`settings: <TrafficManagerSettings json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`rules:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name1: <TrafficManagerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`name2: <TrafficManagerRule json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`...`

* `poll-seconds` (*double*) - how often the proxy manager should poll the `reload` UUID key for changes, updating the proxy definitions when a change is detected.  This defaults to 10 seconds.

* `fallback-poll-seconds` (*double*) - how often the proxy manager should scan TLS certificates persisted in Vault for expiration checks and updates and also poll the individual proxy definitions for changes.  This defaults to 300 seconsds or 5 minutes.

* `cert-warn-days` (*double*) - number of days in advance to begin warning of certificate expirations.

* `proxies/*/proxy-conf` - public or private proxy's generated HAProxy and Varnish configurations as a ZIP archive.

* `proxies/*/proxy-hash` - MD5 hash of the public or private traffic manager's `*-proxy-conf` archive combined with the hash of all of the referenced certificates.  This is used by `neon-proxy` instances to detect when the proxy configuration has changed.

* `status/*` (*json*) - proxy rule status at the time the `neon-proxy-manager` last processed hive rules for the named traffic manager.

* `conf` - root key for proxy settings that need to be monitored for changes.

* `reload` - set to a new UUID whenever a certificate or traffic manager rule is changed.  `neon-proxy-manager` monitors this and republishes HAQProxy configurations when a change is detected.

* `settings` - global (per proxy) settings for a traffic manager formatted as JSON.

* `rules` - named load traffic manager formatted as JSON.

# Vault

`neon-proxy-manager` reads TLS certificates from Vault and includes these in the generated HASProxy configurations.  Vertificates are stored by name as JSON.  Here's where this is located in Vault.

&nbsp;&nbsp;&nbsp;&nbsp;`neon-secret:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert:`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-1: <cert json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`cert-2: <cert json>`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`...`

# Deployment

`neon-proxy-manager` is typically deployed only to manager nodes.  The best practice is to deploy this as a Docker swarm mode service with one replica constrained to manager nodes with `mode=global`.  This relies on Docker to ensure that only one instance is running.

`neon-cli` deploys `neon-proxy-manager` when the hive is provisioned using this Docker command:

````
docker service create \
    --name neon-proxy-manager \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --mount type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock \
    --env VAULT_CREDENTIALS=neon-proxy-manager-credentials \
    --env LOG_LEVEL=INFO \
    --secret neon-proxy-manager-credentials \
    --secret neon-hivemq-neon \
    --constraint node.role==manager \
    --replicas 1 \
    --restart-delay 10s \
    nhive/neon-proxy-manager
````
&nbsp;
