**Do not use: Work in progress**

Proxies internal HTTP API requests to the Vault servers running on the cluster manager nodes.

# Supported Tags

* `latest`

# Configuration

This image implements the **neon-proxy-vault** service which is responsible for forwarding internal requests to the HashiCorp Vault instances running on the cluster manager nodes.  This this function could not be handled by the **neon-proxy-private** service because that service needs to call Vault to retrieve secrets such as TLS certificates and keys. 

This image derives from **neoncloud/haproxy** but deploys its own **haproxy.cfg** file.  It also ignores the `CONFIG_INTERVAL` environment variable, if passed.

The image does require the list of manager node IP addresses and the ports where Vault will be listening.  These must be passed a comma separated `NODE:IP:PORT` entries in the **VAULT_ENDPOINTS** environment variable, like:

&nbsp;&nbsp;&nbsp;&nbsp;`VAULT_ENDPOINTS=manager-0:10.0.1.30:8200,manager-1:10.0.1.31:8200,...`

where `NODE` identifies the Docker host node where the Vault instance is running and `IP:PORT` is the network endpoint.

The load balancer service listens internally on the standard Vault **port 8200** which should be published to the Docker mesh network on port `NeonHostPorts.ProxyVault` (**5003**) to make the service available to Docker hosts.

Note that this image will load environment variables from `/etc/neoncluster/env-host` and `/etc/neoncluster/env-container` if either of these files have been volume mapped into the container.

**IMPORTANT:** 

Do not attempt to mount a HAProxy configuration file to the container at `/etc/haproxy/haproxy.cfg`.  Doing so will cause the container to fail if the file is read/only or else the mapped file will be overwritten.
