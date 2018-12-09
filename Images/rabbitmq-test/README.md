RabbitMQ image intended for unit testing purposes.

# Image Tags

Supported images are tagged with the RabbitMQ version plus the image build date.  All images include the RabbitMQ management features.

# Description

**DO NOT USE FOR PRODUCTION**

This image is intended for development and testing purposes.  It provisions the root virtual host **"/"** and an administrator account with full permissions.  Here are the default settings:

Admin Username: **Administrator**
Admin Password: **password**

The container exposes the AMQP service on its standard port **5672** and the administrative REST API on its standard port **15672**.

# Configuration

RabbitMQ configuration is performed using a subset of the environment variables as described [here](https://www.rabbitmq.com/configure.html).  Most of these have reasonable defaults.

* `RABBITMQ_VM_MEMORY_HIGH_WATERMARK` - (*optional*) specifies the maximum amount of RAM to use as a percentage of available memory (e.g. `0.49` for 49%) or an absolute number of bytes like `1000000000` or `100MB`).  This defaults to `0.50`.

* `RABBITMQ_HIPE_COMPILE` - (*optional*) specifies that RabbitMQ be precompiled using the HiPE Erlang just-in-time compiler for 20-50% better performance at the cost of an additional 30-45 second delay during startup.  Set this to "1" to enable.  This defaults to `0` to disable precompiling.

* `RABBITMQ_DISK_FREE_LIMIT` - (*optional*) specifies the minimum bytes of free disk space available when RabbitMQ will begin throttling messages via flow control.  This is an absolute number of bytes like `1000000000` or `100MB`.  This defaults to twice the available RAM plus `1GB` to help prevent RabbitMQ from filling the disk up on hive host nodes.

* `DEBUG` - (*optional*) currently logs some additional configuration information while starting RabbitMQ.  This defaults to `false`.  Set to `true` to enable.

RabbitMQ persists its operational data to the `/var/lib/rabbitmq` directory within the container.  This will typically be mapped to an unnamed local Docker volume so file I/O performance will not be inhibited by Docker's copy-on-write graph filesystem.

**NOTE:**

After some experimentation, I've seen that setting `RABBITMQ_HIPE_COMPILE=1` requires something like 600MB or memory to start successfully.  Without precompiling, RabbitMQ can start with `350MB` RAM.

# Deployment

Deployment is typically handled transparently by the `RabbitMQFixture` class for unit tests.  This class will start the container using a command like:

```
docker run \
    --detach \
    --name rabbitmq-test \
    --mount type=volume,target=/var/lib/rabbitmq \
    --env RABBITMQ_VM_MEMORY_HIGH_WATERMARK=0.5 \
    --env RABBITMQ_HIPE_COMPILE=0 \
    --env RABBITMQ_DISK_FREE_LIMIT=1GB \
    --env DEBUG=false \
    --publish 5672:5672 \
    --publish 15672:15672 \
    --restart always
```
&nbsp;

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.

* [pwgen](https://linux.die.net/man/1/pwgen) is a password generator.
