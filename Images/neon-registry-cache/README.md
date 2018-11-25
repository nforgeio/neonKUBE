This base image derives from the offical [registry](https://hub.docker.com/_/registry/) and is intended to operate as a pull-thru registry cache for a neonHIVE.

# Image Tags

Supported images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`.

# Description

This image derives from the offical [registry](https://hub.docker.com/_/registry/) and is intended to operate as a pull-thru registry cache for a neonHIVE that can reduce the network traffic to upstream image registries.

# Environment Variables

* `HOSTNAME` (*required*) - hostname for this instance.

* `REGISTRY` (*optional*) - URL of the remote registry being cached.  This defaults to the Docker Public registry at https://registry-1.docker.io.

* `USERNAME` (*optional*) - user ID used to authenticate with the cache and the remote registry.

* `PASSWORD` (*optional*) - password used to authenticate with the cache and the remote registry.

* `LOG_LEVEL` (*optional*) - registry logging level, one of: `error`, `warn`, `info`, or `debug`.  This defaults to `info`.

# Volumes

This image expects two volumes to be mounted to it:

The `etc/neon-registry-cache` directory should be mounted as read-only and must include the cache's TLS certificate and private key files named `cache.crt` and `cache.key`  The `neon-cli` maps this to the same directory on the host when the container is started.

The `var/lib/neon-registry-cache` directory should be mounted as a named read/write Docker volume, especially for production environments.  This is where the cached data will be stored.  `neon-cli` handles this configuration as well.

# Operation

The registry caches are deployed such that hive Docker daemons will attempt to download cached images beginning at the first manager node (as lexigraphically sorted by name).  If this fails, Docker will failover to the next manager.  If all managers fail, then the daemon will download directly from the authoritative external registry.

This configuration makes a NeonCluser self-bootstrapping where even this `neon-registry-cache` image can be deployed during hive setup, even before any other caches have been deployed.

# Deployment

The neonHIVE `neon-cli` handles the deployment of Docker pull-thru registry caches to the hive manager nodes unless disabled in the hive definition.  The tool performs the following steps (documented [here](https://docs.docker.com/registry/insecure/):

1. Generates a self-signed certificate for each hive manager with the certificate hosts matching `MANAGERNAME>.neon-registry-cache.HIVENAME.nhive.io` where *MANAGERNAME* is the name of the manager node and *HIVENAME* identifies the hive.

2. Copies the generated certificates to every hive node as `/etc/docker/certs.d/<hostname>:5002/ca.crt`.

3. Configures Linux on all nodes to trust the certificates as well.

4. Updates `/etc/hosts` on all hive nodes with A records that map each manager node IP address to the corresponding hostname.

5. Configures the Docker systemd unit file with the list of with the manager registry cache URIs followed by the external authoritative registry URI. 

6. Copies the generated certificate and key file into `/etc/neon-registry-cache` on each manager node.

7. Creates a Docker volume named `neon-registry-cache` on each manager node (to be used to host the cached image layers).

8. Runs the registry cache on each hive manager as a container, mapping the host's `/etc/neon-registry-cache` directory as well as the `neon-registry-cache` volume into the container.

# Upgrading

To upgrade the registry cache, you'll need to run the script below on each of your manager nodes to stop the existing cache, pull the new image, and then start the new image.  Note that you'll need to replace `<manager-name>` with the name of the manager node before running the script and set the `REMOTE_URL`, `USER`, and `PASSWORD` environment variables as required for your environment.

````
docker rm -f neon-registry-cache

docker pull nhive/neon-registry-cache:latest

docker run \
    --name neon-registry-cache \
    --detach \
    --restart always \
    --publish 5002:5000 \
    --env HOST=<MANAGER-NAME>.neon-registry-cache.HIVENAME.nhive.io \
    --volume /etc/neon-registry-cache:/etc/neon-registry-cache:ro \
    --volume neon-registry-cache:/var/lib/neon-registry-cache \
    nhive/neon-registry-cache
````
&nbsp;
