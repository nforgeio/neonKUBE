RabbitMQ message queue with the management plugin base image (Alpine) deployed by neonHIVE.

# Image Tags

Supported images are tagged with the RabbitMQ version plus the image build date.  All images include the RabbitMQ management features.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

RabbitMQ (Alpine) image used internally by neonHIVE services and also available for user services.  This image is deployed automatically during hive setup and includes the management plugin.

# Configuration

RabbitMQ configuration is performed using environment variables as described [here](https://www.rabbitmq.com/configure.html).  Most of these have reasonable defaults.  Here a few variables that you'll probably use:

* **CLUSTER_NODES** - (*optional*) specifies the hostnames for all of the RabbitMQ nodes in a cluster, separated by semicolons.  Each hostname must be resolvable.  This is required when setting up a RabbitMQ cluster as opposed to a standalone node.

* **NODE_NAME** - (*optional*) specifies the hostname for the current node.  This is required for a RabbitMQ cluster and must be the same as what was specified in **CLUSTER_NODES**.

* **USERNAME** - (*optiona*) specifies the username used to secure RabbitMQ.  This defaults to **guest**.

* **PASSWORD** - (*optiona*) specifies the password used to secure RabbitMQ.  This defaults to **guest**.

* **RABBITMQ_ERLANG_COOKIE** - (*optional*) shared secret to be used by clustered RabbitMQ nodes for mutual authentication.  This is just a password string.

* **RABBITMQ_VM_MEMORY_HIGH_WATERMARK** - (*optional*) specifies the maximum amount of RAM to use as a percentage of available memory (e.g. **49%** or **0.49**) or an absolute number of bytes like **1000000000** or **100MiB**).

# Deployment

RabbitMQ is deployed automatically during hive setup.  By default, it'll be deployed to hive manager nodes but you can target specific swarm or pet nodes by setting the `io.neonhive.rabbitmq` label to `true` for the target nodes.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
