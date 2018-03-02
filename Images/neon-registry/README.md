This image derives from the offical [registry](https://hub.docker.com/_/registry/) and is intended to operate as a Docker registry for a neonCLUSTER.

# Image Tags

Supported images are tagged with the Git branch, image build date, and Git commit and an optional **-dirty** suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as **latest**.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This image derives from the offical [registry](https://hub.docker.com/_/registry/) and is intended to operate as a Docker Registry for a neonCLUSTER.

**neon-registry** is intended to be deployed as a Docker service or container on a neonCLUSTER with the **Ceph Filesystem** enabled.  **CephFS** implements a shared file system that is available on all cluster nodes as well as to Docker services and containers using the **neon volume driver**.  Registry service instances or containers will all mount the same shared **neon** volume to store the Docker images.  CephFS ensures that all registry instances see the same data and it also provides for data redundancy.

**neon-registry** is provisioned without integrated TLS support as it expects to be deployed behind a neonCLUSTER HTTPS proxy route using a TLS certificate to encrypt traffic.

# Environment Variables

* **USERNAME** (*required*) - user ID used to authenticate with the registry.

* **PASSWORD** (*required*) - password used to authenticate with the registry.

* **SECRET** (*required*) - a cryptographically random string used to persist state to clients to prevent tampering.  You must specify the same value for every registry instance in your cluster.

* **LOG_LEVEL** (*optional*) - registry logging level, one of: `error`, `warn`, `info`, or `debug`.  This defaults to `info`.

* **READ_ONLY** (*optional*) - indicates that the registry should be started as read-only.  This is useful for making the registry read-only during garbage collection.  Possible values are `true` and `false`.  This defaults to `false`.

# Volumes

This image expects a single host volume to be mounted to the container at `/var/lib/registry`.  This is where the registry will persist the image manifests and layers.  For development or test environments with only a single deployed registry instance, this may simply reference a local Docker volume.  For production clusters that deploy multiple registry instances, this must reference a shared file system like one hosted on the integrated CephFS using the **neon volume driver**.

# Deployment

The **neon-registry** image may be deployed as a Docker container or service.  We generally recommend deploying this as a service since that will be easier to manage.

In either case, you'll generally need the following:

1. A DNS host name with the IP address of the registry, like: `registry.mycluster.com`.  This will need to be public if you need to push images from outside your cluster.

2. A TLS certificate for the registry host name.  This should be a real certificate (not self-signed).  [namecheap.com](http://namecheap.com) sells single site certificates for less than $10, so just bite the bullet and purchase one.

3. The username and password that will secure access to the registry.

4. A crytographically generated secret.  You can generate one using `neon create password`.  Note that you'll need to retain this secret somewhere in case you'll need to redeploy the registry container or service in the future.

## Deploy as a Service

## Deploy as a Container

# Upgrading

To upgrade the registry cache, you'll need to run the script below on each of your manager nodes to stop the existing cache, pull the new image, and then start the new image.  Note that you'll need to replace `<manager-name>` with the name of the manager node before running the script and set the `REMOTE_URL`, `USER`, and `PASSWORD` environment variables as required for your environment.

````
docker rm -f neon-registry-cache

docker pull neoncluster/neon-registry-cache:latest

docker run \
    --name neon-registry-cache \
    --detach \
    --restart always \
    --publish 5002:5000 \
    --env HOST=<MANAGER-NAME>.neon-registry-cache.cluster \
    --volume /etc/neon-registry-cache:/etc/neon-registry-cache:ro \
    --volume neon-registry-cache:/var/lib/neon-registry-cache \
    neoncluster/neon-registry-cache
````
&nbsp;
