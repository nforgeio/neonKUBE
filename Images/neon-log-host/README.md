**Do not use: Work in progress**

Deployed as the **neon-log-host** service to all NeonCluster nodes to capture host logs.

# Supported Tags

* `1.0.0, latest`

# Description

The **neon-log-host** image as a local container is deployed on all cluster nodes (both managers and workers) to perform these functions:

* Capturing local systemd journal events.
* Capturing local syslog events.
* Receiving container events forwarded by the local Docker daemon via the **fluent** log driver.
* Forwarding events on to the **neon-log-collector** service.

The **neon-log-collector** service is responsible for receiving events from the hosts and then:

* Filtering out undesired events.
* Normalizing events into standard formats.
* Persisting events to the log Elasticsearch cluster.
* Optionally forwarding events to external log pipelines.

# Included TD-Agent plugins

* [systemd (input)](https://github.com/reevoo/fluent-plugin-systemd/blob/master/README.md) This plug-in can read the systemd journal.

# Deployment

This image requires some knowledge about its execution context like the cluster and node names.  There's currently no standard way to provide this in Docker so the container requires that the `/etc/neoncluster/env-host` script on the host be mounted to the same location within the container and that this script initializes some standard `NEON_*` environment variables.

The image also requires **read/write** access to the host's `/var/log` directory so it can access the systemd logs and also to persist some state. 

The **neon-cli** cluster management tool handles the container deployment during cluster setup by running the following command on every cluster node:

````
docker run \
    --name neon-log-host \
    --detach \
    --restart always \
    --volume /etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    --volume /var/log:/hostfs/var/log \
    --network host \
    --log-driver json-file \
    neoncluster/neon-log-host
````
&nbsp;
# Extending or Replacing this Image

You may find it necessary to modify or replace the behavior of the **neon-log-host** containers.

You can modify or extend this by creating a new image that derives from this one, installing any required TD-Agent plugins and then modifying the `td-agent.conf` file.  For new clusters, you can specify the new collector image in the cluster definition before deployment.  For existing clusters, you'll need to submit the following Docker commands to replace the **neon-log-host** containers on every node:

````
docker rm -f neon-log-host

docker pull neoncluster/neon-log-host

docker run \
    --name neon-log-host \
    --detach \
    --restart always \
    --volume /etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    --volume /var/log:/hostfs/var/log \
    --network host \
    --log-driver json-file \
    neoncluster/neon-log-host
````
