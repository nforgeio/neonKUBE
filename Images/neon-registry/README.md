This image derives from the official [registry](https://hub.docker.com/_/registry/) and is intended to operate as a Docker registry for a neonHIVE.

# Image Tags

Supported images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`

# Description

This image derives from the official [registry](https://hub.docker.com/_/registry/) and is intended to operate as a Docker registry for a neonHIVE.

`neon-registry` is intended to be deployed as a Docker service or container on a neonHIVE with the **Ceph Filesystem** enabled.  **`CephFS** implements a shared file system that is available on all hive nodes as well as to Docker services and containers using the `neon volume driver`.  Registry service instances or containers will all mount the same shared `neon` volume to store the Docker images.  CephFS ensures that all registry instances see the same data and it also provides for data redundancy.

`neon-registry` is provisioned without integrated TLS support as it expects to be deployed behind a neonHIVE HTTPS proxy route using a TLS certificate to encrypt traffic.

# Environment Variables

* `USERNAME` (*required*) - user ID that clients will use to authenticate with the registry.

* `PASSWORD` (*required*) - password that clients will use to authenticate with the registry.

* `SECRET` (*required*) - a cryptographically random string used to persist state to clients to prevent tampering.  You must specify the same value for every registry instance in your hive.

* `READ_ONLY` (*optional*) - indicates that the registry should be started as read-only.  This is useful for making the registry read-only during garbage collection.  Possible values are `true` and `false`.  This defaults to `false`.

* `LOG_LEVEL` (*optional*) - registry logging level, one of: `error`, `warn`, `info`, or `debug`.  This defaults to `info`.

# Volumes

This image expects a single host volume to be mounted to the container at `/var/lib/neon-registry`.  This is where the registry will persist the image manifests and layers.  For development or test environments with only a single deployed registry instance, this may simply reference a local Docker volume.  For production hives that deploy multiple registry instances, this must reference a shared file system like one hosted on the integrated CephFS using the **neon volume driver**.

# Deployment

The `neon-registry` image may be deployed as a Docker container or service.  We generally recommend deploying this as a service since that will be easier to manage.

In either case, you'll generally need the following:

1. A DNS hostname with the IP address of the registry, like: `REGISTRY.MY-HIVE.COM`.  This will need to be public if you need to push images from outside your hive.

2. A TLS certificate for the registry hostname.  This should be a real certificate (not self-signed).  [namecheap.com](http://namecheap.com) sells single site certificates for less than $10, so just bite the bullet and purchase one.

3. The username and password to secure access to the registry.

4. A crytographically generated secret.  You can generate one using `neon create password`.  Note that you'll need to retain this secret somewhere in case you'll need to redeploy the registry container or service in the future.

You'll typically want to have the registry listen on the default port `5000` which is reserved for this purpose on neonHIVE hosts.

## Deploy as a Service

The Docker service command below shows how `neon-registry` can be deployed as a service.  In this example, we're deploying the registry on each of the manager nodes persisting data using the `neon` volume plugin to the `neon-registry` volume.

**NOTE* ** You'll need to replace *MY-USER*, *MY-PASSWORD*, and *MY-SECRET* with the required values for your environment.

```
docker service create \
    --name neon-registry \
    --detach=false \
    --mode global \
    --constraint node.role==manager \
    --env USERNAME=MY-USER \
    --env PASSWORD=MY-PASSWORD \
    --env SECRET=MY-SECRET \
    --env LOG_LEVEL=info \
    --env READ_ONLY=false \
    --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
    --network neon-public \
    --restart-delay 10s \
    nhive/neon-registry
```
&nbsp;
Next, you'll need to save your TLS certificate to neonHIVE:

```
neon proxy public put MY-CERT-NAME PATH-TO-CERT
```
&nbsp;
Finally, you'll need to deploy a proxy route that will direct traffic from the host network to the service.  Your route file will look something like this (as YAML):

```
name: neon-registry
mode: http
frontends:
- host: REGISTRY.MY-HIVE.COM
  certname: MY-CERT-NAME
backends:
- server: neon-registry
  port: 5000
```
&nbsp;

This route accepts HTTPS requests on the standard public SSL port on all of the hive hosts, handles TLS termination and then forwards the requests to the `neon-registry` service as unencrypted HTTP on service port 5000.

## Deploy as a Container

You can also deploy `neon-registry` as a container.  We recommend deploying this as a service, but sometimes it's necessary to deploy containers (e.g. to dedicated pet nodes).  The steps are similar to those for deploying as a service:

**NOTE* ** You'll need to replace *MY-USER*, *MY-PASSWORD*, and *MY-SECRET* with the required values for your environment.

First, start the containers like:

```
docker run \
    --name neon-registry \
    --detach \
    --env USERNAME=MY-USER \
    --env PASSWORD=MY-PASSWORD \
    --env SECRET=MY-SECRET \
    --env LOG_LEVEL=info \
    --env READ_ONLY=false \
    --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
    --publish 6000:5000 \
    --restart always \
    nhive/neon-registry
```
&nbsp;
Next, you'll need to save your TLS certificate to neonHIVE:

```
neon proxy public put MY-CERT-NAME PATH-TO-CERT
```
&nbsp;
Finally, you'll need to deploy a proxy route that will direct traffic from PORT 6000 on the host network to the container instances.  You'll need to know the IP addresses the nodes where you deployed the containers.  Your route file will look something like this (as YAML):

```
name: neon-registry
mode: http
frontends:
- host: REGISTRY.MY-HIVE.COM
  certname: MY-CERT-NAME
  proxyport: 5000
backends:
- server: NODE1-IP
  port: 6000
- server: NODE2-IP
  port: 6000
- server: NODE2-IP
  port: 6000
```
&nbsp;

This example route assumes that you've deployed `neon-registry` as a container to three nodes whose IP addresses are NODE1-IP, NODE2-IP, and NODE3-ip.  The route accepts HTTPS requests on port 5000 on all of the hive hosts, handles TLS termination and then load balances the requests across the three containers as unencrypted HTTP to port 6000 published by the containers and Docker will forward these to port 5000 inside the container.

# Garbage Collection

`neon-registry` service instances or containers don't automatically prune unreferenced image layers.  This means that deleting an image manifest **does not** delete the referenced images.  Image layers can accumulate until you fill up the file system.

Production hives should perform garbage collection from time-to-time to address this.  This can be accomplished by running the image with the `garbage-collect` command.  The only constraint is that all of the `neon-registry` instances must be stopped or running as *read-only*.

**WARNING:** Performing garbage collection with read/write registries risks image corruption.

For a registry deployed as a Docker service, garbage collection can be performed via:

```
docker service update --env READ_ONLY=true neon-registry

docker run \
   --name neon-registry-prune \
   --restart-condition=none \
   --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
   nhive/neon-registry garbage-collect

docker service update --env READ_ONLY=false neon-registry
```
&nbsp;

These commands restarts the registry instances as *read-only*, runs the garbage collector as a container, and then restarts the registry instances as *read/write*.

Garbage collection for registries deployed as containers will work much the same except that you'll need to handle restarting the containers as *read-only* and *read/write* manually or using scripts.
