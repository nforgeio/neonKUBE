RabbitMQ message queue with the management plugin base image deployed to provide messaging services for neonHIVE.

# Image Tags

Supported images are tagged with the RabbitMQ version plus the image build date.  All images include the RabbitMQ management features.

# Description

RabbitMQ image used internally by neonHIVE services and also available for user services.  This image is deployed automatically during hive setup and includes the management plugin.

# Configuration

RabbitMQ configuration is performed using environment variables as described [here](https://www.rabbitmq.com/configure.html).  Most of these have reasonable defaults.  Here a few variables that you'll probably use:

* `CLUSTER_NAME` - (*optional*) specifies the cluster name.

* `CLUSTER_NODES` - (*optional*) specifies the node name and resolvable hostname (like `NODENAME@HOSTNAME`) for all of the RabbitMQ nodes in a cluster, separated by commas for each cluster node.  Each hostname must be resolvable.  This is required when setting up a RabbitMQ cluster as opposed to a standalone node.

* `CLUSTER_PARTITION_MODE` - (*optional*) specifies how RabbitMQ handles [network partitions](https://www.rabbitmq.com/partitions.html).  This defaults to `autoheal` which favors cluster availability over the possibility of data loss.  The nice thing about `autoheal` is that it automatically handles the common scenarios (hive shutdown, power failures, individual hive node reboots, etc.)  You may also specify `pause_minority` or `pause_if_all_down`, but these may require manual intervention to bring the cluster back online.

* `NODENAME` - (*required* if `CLUSTER_NODES` is present) specifies the fully qualified hostname for the current node (including the *name@* prefix).  This is required for a RabbitMQ cluster and must be the same as what was specified in `CLUSTER_NODES`.

* `ERL_EPMD_PORT` - (*optional*) specifies the port used by the Erlang node discovery protocol.  This defaults to `4369`.

* `RABBITMQ_DEFAULT_USER` - (*optional*) specifies the username used to secure RabbitMQ.  This defaults to `sysadmin`.

* `RABBITMQ_DEFAULT_PASS` - (*optional*) specifies the password used to secure RabbitMQ.  This defaults to `password`.

* `RABBITMQ_NODE_PORT` - (*optional*) specifies the broker's public API port.  This defaults to `5672`.

* `RABBITMQ_DIST_PORT` - (*optional*) specifies the internal port used by RabbitMQ cluster nodes to communicate amongst each other.  This defaults to `25672`.

* `RABBITMQ_MANAGEMENT_PORT` - (*optional*) specifies the internal port used by RabbitMQ cluster nodes to expose the management dashboard.  This defaults to `15672`.

* `RABBITMQ_ERLANG_COOKIE` - (*required* if `CLUSTER_NODES` is present) shared secret to be used by clustered RabbitMQ nodes for mutual authentication.  This is just a password string.

* `RABBITMQ_VM_MEMORY_HIGH_WATERMARK` - (*optional*) specifies the maximum amount of RAM to use as a percentage of available memory (e.g. `0.49` for 49%) or an absolute number of bytes like `1000000000` or `100MB`).  This defaults to `0.50`.

* `RABBITMQ_HIPE_COMPILE` - (*optional*) specifies that RabbitMQ be precompiled using the HiPE Erlang just-in-time compiler for 20-50% better performance at the cost of an additional 30-45 second delay during startup.  Set this to "1" to enable.  This defaults to `0` to disable precompiling.

* `RABBITMQ_DISK_FREE_LIMIT` - (*optional*) specifies the minimum bytes of free disk space available when RabbitMQ will begin throttling messages via flow control.  This is an absolute number of bytes like `1000000000` or `100MB`.  This defaults to twice the available RAM plus `1GB` to help prevent RabbitMQ from filling the disk up on hive host nodes.

* `RABBITMQ_SSL_CERTFILE` - (*optional*) enables TLS security for both the AMQP endpoint as well as the RabbitMQ dashboard by specifying the path to the TLS certificate file.

* `RABBITMQ_SSL_KEYFILE` - (*optional*) enables TLS security for both the AMQP endpoint as well as the RabbitMQ dashboard by specifying the path to the TLS private key file.

* `MANAGEMENT_PLUGIN` - (*optional*) enables the RabbitMQ management plugin for the node.  This defaults to `false`.

* `DEBUG` - (*optional*) currently logs some additional configuration information while starting RabbitMQ.  This defaults to `false`.  Set to `true` to enable.

RabbitMQ persists its operational data to the `/var/lib/rabbitmq` directory within the container.  Production deployments will typically mount a named Docker volume here so the data will persist across container restarts.

**NOTE:**

After some experimentation, I've seen that setting `RABBITMQ_HIPE_COMPILE=1` requires something like 600MB or memory to start successfully.  Without precompiling, RabbitMQ can start with `350MB` RAM.

# Clustering

RabbitMQ is often deployed as a cluster of multiple instances for resilience and scalability.  You need to do two things to make this work:

1. Generate a password string and assign it to `RABBITMQ_ERLANG_COOKIE` for each RabbitMQ instance so they can mutually authenticate each other.
2. Implement a node discovery mechanism.

The simplest way to configure node discovery is to pass the list of nodes in `CLUSTER_NODES` when launching each RabbitMQ instance also passing the specific RabbitMQ node name in `NODENAME`.  This statically configures the cluster using the RabbitMQ configuration file.

Here's an example that creates a two node cluster on two different machines `server-0.mydomain.com` and `server-1.mydomain.com`:
```
# Set the secret used to mutually authenticate the nodes.
cluster_secret=shared-secret

# Create the [myrabbit-0] RabbitMQ node on the [server-0.mydomain.com] server.
docker run \
    --detach \
    --name neon-hivemq \
    --env CLUSTER_NAME=my-message-cluster \
    --env CLUSTER_NODES=myrabbit-0@server-0.mydomain.com,myrabbit-1@server-1.mydomain \
    --env CLUSTER_PARTITION_MODE=autoheal \
    --env NODENAME=myrabbit-0@server-0.mydomain.com \
    --env RABBITMQ_ERLANG_COOKIE=$cluster_secret \
    --env RABBITMQ_SSL_CERTFILE=/etc/neon/certs/hive.crt \
    --env RABBITMQ_SSL_KEYFILE=/etc/neon/certs/hive.key \
    --mount type=volume,source=neon-hivemq,target=/var/lib/rabbitmq \
    --mount type=bind,source=/etc/neon/certs,target=/etc/neon/certs,readonly \
    --restart always

# Create the [myrabbit-1] RabbitMQ node on the [server-1.mydomain.com] server.
docker run \
    --detach \
    --name neon-hivemq \
    --env CLUSTER_NAME=my-message-cluster \
    --env CLUSTER_NODES=myrabbit-0@server-0.mydomain.com,myrabbit-1@server-1.mydomain \
    --env CLUSTER_PARTITION_MODE=autoheal \
    --env NODENAME=myrabbit-1@server-1.mydomain.com \
    --env RABBITMQ_ERLANG_COOKIE=$cluster_secret \
    --env RABBITMQ_SSL_CERTFILE=/etc/neon/certs/hive.crt \
    --env RABBITMQ_SSL_KEYFILE=/etc/neon/certs/hive.key \
    --mount type=volume,source=neon-hivemq,target=/var/lib/rabbitmq \
    --mount type=bind,source=/etc/neon/certs,target=/etc/neon/certs,readonly \
    --restart always
```
&nbsp;
Another more flexible way, is to start the nodes and then use the `rabbitmqctl` CLI to form the cluster as discussed [here](https://www.rabbitmq.com/clustering.html#transcript).

**NOTE:** The static cluster configuration specified by `CLUSTER_NODES` is used only when a RabbitMQ node is started for the first time or reset.  It is possible to use `rabbitmqctl` to reconfigure these clusters after they have started.

# Hive Deployment

RabbitMQ is deployed automatically as one or more containers during hive setup.  By default, it'll be deployed to hive managers but you can target specific swarm or pet nodes by setting the `io.neonhive.hivemq` and/or `io.neonhive.hivemq-manager` label to `true` for the target nodes.

Each RabbitMQ container will be named `neon-hivemq` and will also have a local named volume `neon-hivemq` for persistent storage.  A local hive DNS entry for each container will defined like `NODENAME.neon-hivemq.HIVENAME.nhive.io`, where *NODENAME* is the name of the hosting node and *HIVENAME* is the have name.  The management components are always enabled.

The hive deployed containers also use these settings:

`ERL_EPMD_PORT=5009`
`RABBITMQ_NODE_PORT=5010`
`RABBITMQ_DIST_PORT=5011`
`RABBITMQ_MANAGEMENT_PORT=5012`

Each built-in RabbitMQ instance deployed for the hive will have mount a local Docker volume named `neon-hivemq`.  This will be mapped to the `/var/lib/rabbitmq` directory within the container so that the message related data will persist across container restarts.

neonHIVEs default to deploying RabbitMQ without precompiling and allocating 250MB RAM per each instance deployed to hive manager nodes.  We do this to reduce the overhead for very small development or testing hives.

For production scenarios that use messaging heavily, you should consider enabling `RABBITMQ_HIPE_COMPILE=1` which will increase the default RabbitMQ RAM to 500MB and also consider deploying dedicated hive pet nodes to host RabbitMQ and also increase the allocated RAM to make best use of these pets.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.

* [pwgen](https://linux.die.net/man/1/pwgen) is a password generator.
