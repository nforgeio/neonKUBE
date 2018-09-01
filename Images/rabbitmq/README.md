RabbitMQ message queue with the management plugin base image deployed by neonHIVE.

# Image Tags

Supported images are tagged with the RabbitMQ version plus the image build date.  All images include the RabbitMQ management features.

From time-to-time you may see images tagged like `:BRANCH-*` where *BRANCH* identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

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

* `RABBITMQ_VM_MEMORY_HIGH_WATERMARK` - (*optional*) specifies the maximum amount of RAM to use as a percentage of available memory (e.g. `49%` or `0.49`) or an absolute number of bytes like `1000000000` or `100MiB`).

* `RABBITMQ_HIPE_COMPILE` - (*optional*) specifies that RabbitMQ be precompiled using the HiPE Erlang just-in-time compiler for 20-50% better performance at the cost of an additional 30-45 second delay during startup.  Set this to "1" to enable.  This defaults to `0` to disable precompiling.

RabbitMQ persists its operational data to the `/var/lib/rabbitmq/mnesia` directory within the container.  Production deployments will typically mount a named Docker volume here so the data will persist across container restarts.

# Hive Deployment

RabbitMQ is deployed automatically as one or more containers during hive setup.  By default, it'll be deployed to hive managers but you can target specific swarm or pet nodes by setting the `io.neonhive.rabbitmq` label to `true` for the target nodes.

Each RabbitMQ container will be named `neon-rabbitmq` and will also have a local named volume `neon-rabbitmq` for persistent storage.  A local hive DNS entry for each container will defined like `NODENAME.neon-rabbitmq.HIVENAME.nhive.io`, where *NODENAME* is the name of the hosting node and *HIVENAME* is the have name.

The RabbitMQ service will be configured to listen on `RABBITMQ_NODE_PORT` for the public AMQP API.  This defaults to `5672` but will be set to `5009` for the built-in hive brokers and published by the container.  Clustered nodes use port 20000 by default to communicate priviately between themselves.  This is published as port `5010` one the hive hosts.

The RabbitMQ management UI plugin will be enabled.  This listens on `15672` within the container but is published as `5011` for external access.

Each built-in RabbitMQ instance deployed for the hive will have mount a local Docker volume named `neon-rabbitmq`.  This will be mapped to the `/var/lib/rabbitmq` directory within the container so that the message related data will persist across container restarts.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.

* [pwgen](https://linux.die.net/man/1/pwgen) is a password generator.
