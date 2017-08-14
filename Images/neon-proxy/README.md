**Work in progress: Do not use**

This is the standard neonCLUSTER network Proxy service based on **HAProxy**, **Consul**, and **Vault**.  This is typically deployed alongside the **neon-proxy-manager** service that monitors changes to proxy routes and TLS certificates to regenerate the HAProxy configuration.

# Supported Tags

* `1.6.9`
* `1.6.10`
* `1.7.0`
* `1.7.1`
* `1.7.2`
* `1.7.7, latest`

# Additional Packages

This image includes the following packages:

* **jq** JSON parser
* **unzip** zip archive utilities
* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.

# Basic Configuration

This image dynamically configures HAProxy by retrieving configuration data persisted to a Consul key.  The data is a binary ZIP archive including the `haproxy.cfg` text file.  This is formatted as standard HAProxy configuration file.  The ZIP archive may also include other files such as TLS public/private keys as well as the optional `.certs` file that specifies any certificates to be obtained from Vault.

All you need to do is pass the **CONFIG_KEY** environment variable as the Consul key where the configuration ZIP archive will be saved.  Here's a logical overview of how this works:

1. The key value is retrieved from Consul and persists it to `/tmp/secrets/haproxy/haproxy.zip`.  The container exits if the value can't be retrieved.

2. `/tmp/secrets/haproxy/haproxy.zip` is unzipped to the same directory.

3. `/tmp/secrets/haproxy/haproxy.cfg` is validated. The container exits if it is not.

4. HAProxy is started using the configuration.

5. The key is monitored for changes.  When a change is detected, steps 1-4 above are repeated with the exception that upon any error, the container will log the problem and continue running with the last good configuration.

# Environment Variables

* **CONFIG_KEY** (*required*) - Consul key holding the HAProxy ZIP archive configuration.

* **VAULT_CREDENTIALS** (*required*) - names the file within `/run/secrets/` that holds the Vault credentials the proxy will need to access TLS certificates.

* **WARN_SECONDS** (*optional*) - seconds between logging warning while HAProxy is running with an out-of-date configuration.  This defaults to 300 (5 minutes).

* **START_SECONDS** (*optional*) - seconds to give the chance HAProxy to start cleanly before processing configuration changes.  This defaults to 10 seconds.

* **LOG_LEVEL** (*optional*) - logging level: `CRITICAL`, `ERROR`, `WARN`, `INFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

* **DEBUG** (*optional*) - set to `true` to enable debug mode.  In this mode, the service will not delete the proxy configuration and certificate after HAProxy loads them into memory so these can be examined for debugging purposes.  HAProxy will also be started in debug mode so that it will emit extensive activity information to standard output. Thid dhould nto be enabled for production..

# HAProxy ZIP Archive

This image expects the Consul **CONFIG_KEY** key value to be a ZIP archive holding the `haproxy.cfg` configuration file as well as another assets such as site certificate files.  `haproxy.cfg` file format is described at [haproxy.org](http://www.haproxy.org/#docs).

To use TLS certificates or certificate authorities, you'll need to use the **HAPROXY_CONFIG_FOLDER** environment variable when specifying the base locations.  This is set to the path to the folder where the HAProxy configuration archive contents were extracted.

For example, the configuration fragment below specifies that certificate authorities and certificates are to be loaded from this folder:

&nbsp;&nbsp;&nbsp;&nbsp;`global`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`ca-base "${HAPROXY_CONFIG_FOLDER}"`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`crt-base "${HAPROXY_CONFIG_FOLDER}"`

You should also specify a DNS resolvers section that points to the embedded Docker DNS server so you'll be able to resolve Docker service names.  You can use the **NeonClusterConst_DockerDnsEndpoint** environment variable for this (which is set to `127.0.0.11:53`), like:

&nbsp;&nbsp;&nbsp;&nbsp;`resolvers docker`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`nameserver docker "${NeonClusterConst_DockerDnsEndpoint}"`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`resolve_retries 3`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`timeout retry 1s`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`hold valid 10s`

**NOTE:** `haproxy.cfg` must be authored such that any referenced files are expected to be in the same directory as the configuration file.

# RAM Drive & Security

As a best security practice, we highly recommend that you mount a R/W RAM Drive at `/tmp/secrets`.  This will prevent any secrets unzipped from the HAProxy ZIP archive (or retrieved from Vault) from ever being persisted to a physical disk.  You can accomplish this adding the following to `docker service create` or `docker run`:

&nbsp;&nbsp;&nbsp;&nbsp;`--mount type=tmpfs,target=/tmp/secrets,tmpfs-size=5M,tmpfs-mode=700`

# Advanced Configuration

When deployed as a Docker service, this image can also be used to load secrets such as TLS certificate keys from HashiCorp Vault for even better security.  This is accomplished by adding the `.certs` file to the ZIP archive.  This is a text file that lists the Vault certificates keys to be retrieved and as well as the directory and file name where each certificate will be persisted.  This file is formatted with each line specifying the Vault key, relative directory and file name, each separated with a space, like:

&nbsp;&nbsp;&nbsp;&nbsp;`secret/certs/mycert1 mydir1 mycert1.pem`
&nbsp;&nbsp;&nbsp;&nbsp;`secret/certs/mycert2 mydir2 mycert2.pem`
&nbsp;&nbsp;&nbsp;&nbsp;`...`

When the `.certs` file is present, the container will retrieve the Vault keys and write them to `/tmp/secrets/haproxy` using the specified file names before validating the configuration and starting HAProxy.

Credentials are required to access Vault.  These credentials will be passed as JSON using the **Docker secrets** feature and will be persisted within the container at `/run/secrets/${VAULT_CREDENTIALS}`, where **VAULT_CREDENTIALS** is an environment variable that identifies the secret file.

Two types of credentials are currently supported: **vault-token** and **vault-approle**.

**token:**
&nbsp;&nbsp;&nbsp;&nbsp;`{`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"Type": "vault-token",`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"VaultToken": "65b74ffd-842c-fd43-1386-f7d7006e520a"`
&nbsp;&nbsp;&nbsp;&nbsp;`}`

**approle:**
&nbsp;&nbsp;&nbsp;&nbsp;`{`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"Type": "VaultRoleId": "db02de05-fa39-4855-059b-67221c5c2f63",`
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"VaultSecretId": "6a174c20-f6de-a53c-74d2-6018fcceff64"`
&nbsp;&nbsp;&nbsp;&nbsp;`}`

**NOTE:** This container relies on an external agent to touch the Consul configuration key whenever one or more Vault secrets has changed, for example when a TLS certificate is renewed.  This is typically handled by the **neon-cli** cluster management tool.

**NOTE:** By default, the image will use HAProxy's graceful stop (`-sf` option) when loading a new configuration.  This nearly eliminates the change of dropping active connections during the transition.  There can be scenarios though, where older instances of HAProxy cannot be terminated due to long-lived TCP connections.  You can enable HAProxy hard stop (`-st` option) by including a `.hardstop` file in the configuration archive (the contents don't matter).  Hard stop terminates the existing HAProxy process before starting the new one, dropping any active connections.

**NOTE:** The Consul limit for key values is 512KB.  This could become a limitation for very complex clusters that include TLS certificates in the configuration ZIP archive.  This can be mitigated by persisting certificates in Vault instead (which a better security practice anyway).

**NOTE**: Note that this image will load environment variables from `/etc/neoncluster/env-host` and `/etc/neoncluster/env-container` if either of these files have been mounted mapped to the container.

# Deployment

Proxies are deployed  by default to non-manager nodes (if there are any).  Here are the default deployment commands:

````
docker service create --name neon-proxy-public \
    --mount type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --env CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/conf \
    --env VAULT_CREDENTIALS=neon-proxy-public-credentials \
    --env LOG_LEVEL=INFO \
    --env DEBUG=false \
    --publish 5100-5299:5100-5299 \
    --secret neon-proxy-public-credentials \
    --constraint node.role!=manager \
    --mode global \
    --restart-delay 10s \
    --network neon-cluster-public \
    neoncluster/neon-proxy

docker service create --name neon-proxy-private \
    --mount type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --env CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/conf \
    --env VAULT_CREDENTIALS=neon-proxy-private-credentials \
    --env LOG_LEVEL=INFO \
    --env DEBUG=false \
    --publish 5300-5499:5300-5499 \
    --secret neon-proxy-public-credentials \
    --constraint node.role!=manager \
    --mode global \
    --restart-delay 10s \
    --network neon-cluster-private \
    neoncluster/neon-proxy
````
&nbsp;
**NOTE:** You can modify the scheduling constraints using standard Docker service commands.
