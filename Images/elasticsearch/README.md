**DO NOT USE: Work in progress**

# Image Tags

The following image tags identify deprecated images which will be deleted sometime in the future.

* `5.2.0`&nbsp;&nbsp;&nbsp;&nbsp;`<-- `last image based on [elasticsearch](https://hub.docker.com/_/elasticsearch/)
* `5.3.0`&nbsp;&nbsp;&nbsp;&nbsp;`<-- `first image based directly on [openjdk](https://hub.docker.com/_/openjdk/)
* `5.4.0`
* `5.5.0`

Supported images are tagged with the Elasticsearch version plus the image build date.

**IMPORTANT:**

The official [elasticsearch](https://hub.docker.com/_/elasticsearch/) image is being deprecated in favor of images maintained in Elastic's corporate repository.  The original official images were based on [openjdk](https://hub.docker.com/_/openjdk/).  I'm not entirely sure what the new images are based on, but they seem locked down and my custom scripts no longer work.

The `5.2.0` image here is still based on the original Elasticsearch 5.2.0 image.  Later images are based directly on [openjdk](https://hub.docker.com/_/openjdk/) with Elasticsearch downloaded and installed.  The new image layout is very similar to the original official image.

**NOTE:**

Elasticsearch, Kibana, and Metricbeat are designed to run together as a combined system.  You should deploy the same version of each component to your cluster and when it's time to upgrade, always upgrade the Elasticsearch cluster first, followed by the Metricbeat and Kibana.

# Description

This image hosts [Elasticsearch](https://www.elastic.co/guide/en/elasticsearch/reference/current/getting-started.html) which is typically deployed to a neonCLUSTER for holding the cluster node/container logs and metrics.  This database combined with Kibana form a powerful tool for cluster operators and developers.

# Configuration

You need to specify the following environment variables when running the container (these are referenced by the **elasticsearch.yml** configuration file):

* **ELASTICSEARCH_CLUSTER** (*required*) - identifies the Elasticsearch cluster

* **ELASTICSEARCH_NODE_MASTER** (*optional*) - indicates that this eligible to be a master (defaults to **true**)

* **ELASTICSEARCH_NODE_DATA** (*optional*) - indicates that this node will host data vs. being a dedicated master or just a router (defaults to **true**)

* **ELASTICSEARCH_TCP_PORT** (*required*) - Inter-node TCP communication ports

* **ELASTICSEARCH_HTTP_PORT** (*required*) - HTTP API port

* **ELASTICSEARCH_NODE_COUNT** (*required*) - number of nodes in the cluster

* **ELASTICSEARCH_QUORUM** (*required*) - minimum number of master nodes to be present for the cluster to be considered healthy.

* **ELASTICSEARCH_BOOTSTRAP_NODES** (*required*) - comma separated list of one or more IP addresses or DNS names of nodes that will be used for bootstrapping the cluster.

* **ES_JAVA_OPTS** (*optional*) - Elasticsearch related Java runtime options. ([reference](https://www.elastic.co/guide/en/elasticsearch/reference/current/heap-size.html))

This container is designed be run as a standard Docker container connected to the host's network `--network host`.  This container cannot not be deployed as a Docker swarm mode service because:

* ZEN discovery gets confused when it sees its own pings returning on an unexpected interface.  There is a workaround but its unlikely to work the same from one Docker release to another.  This breaks cluster formation.

* Stateful services like this require more TLC than a service rolling update provides.

# Data Volume

For production deployments, you should mount a Docker host volume to the to the container at:

&nbsp;&nbsp;&nbsp;&nbsp;`/mnt/esdata`

This is where Elasticsearch will persist its indexes. There are at least three good reasons for using a Docker volume:

* **Upgradability** Your data remains available so you can replace your database container with an upgraded version.
* **Managebility** Having your data in an external volume will make it much easier to access, copy, or backup your data.
* **Reliability** The Docker graph file systems aren't really designed for heavy database usage.

# Deployment

Here's are some example Docker commands that deploy a three node Elasticsearch cluster with their data hosted in Docker volumes with names that match the containers on the Docker hosts with these IP addresses: 10.0.0.2, 10.0.0.3, 10.0.0.4.

````
# Create the data volumes.

docker volume create es-node-0
docker volume create es-node-1
docker volume create es-node-2

# Start the containers.

docker run --detach --name es-node-0 \
    --restart always \
    --volume es-node-0:/mnt/esdata \
    --env ELASTICSEARCH_CLUSTER=my-cluster \
    --env ELASTICSEARCH_NODE_DATA=true \
    --env ELASTICSEARCH_NODE_COUNT=3 \
    --env ELASTICSEARCH_TCP_PORT=9300 \
    --env ELASTICSEARCH_HTTP_PORT=9200 \
    --env ELASTICSEARCH_QUORUM=2 \
    --env ELASTICSEARCH_BOOTSTRAP_NODES=10.0.0.2,10.0.0.3,10.0.0.4 \
    --network host \
    neoncluster/elasticsearch

docker run --detach --name es-node-1 \
    --restart always \
    --volume es-node-1:/mnt/esdata \
    --env ELASTICSEARCH_CLUSTER=my-cluster \
    --env ELASTICSEARCH_NODE_DATA=true \
    --env ELASTICSEARCH_NODE_COUNT=3 \
    --env ELASTICSEARCH_TCP_PORT=9300 \
    --env ELASTICSEARCH_HTTP_PORT=9200 \
    --env ELASTICSEARCH_QUORUM=2 \
    --env ELASTICSEARCH_BOOTSTRAP_NODES=10.0.0.2,10.0.0.3,10.0.0.4 \
    --network host \
    neoncluster/elasticsearch

docker run --detach --name es-node-2 \
    --restart always \
    --volume es-node-2:/mnt/esdata \
    --env ELASTICSEARCH_CLUSTER=my-cluster \
    --env ELASTICSEARCH_NODE_DATA=true \
    --env ELASTICSEARCH_NODE_COUNT=3 \
    --env ELASTICSEARCH_TCP_PORT=9300 \
    --env ELASTICSEARCH_HTTP_PORT=9200 \
    --env ELASTICSEARCH_QUORUM=2 \
    --env ELASTICSEARCH_BOOTSTRAP_NODES=10.0.0.2,10.0.0.3,10.0.0.4 \
    --network host \
    neoncluster/elasticsearch

````
&nbsp;
Note that we needed to know the IP address or DNS host names of some Docker nodes hosting the Elasticsearch instances and pass these in `ELASTICSEARCH_BOOTSTRAP_NODES` so the instances can discover each other and form the cluster.
