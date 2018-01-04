# Supported Tags

Supported are be tagged with the Kibana version plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

The following image tags identify archived images which will not be deleted but are no longer maintained.

* `5.2.0`&nbsp;&nbsp;&nbsp;&nbsp;`<-- `last image based on [elasticsearch](https://hub.docker.com/_/kibana/)
* `5.3.0`&nbsp;&nbsp;&nbsp;&nbsp;`<-- `first image based directly on [openjdk](https://hub.docker.com/_/openjdk/)
* `5.4.0`
* `5.5.0`

**IMPORTANT:**

The official [kibana](https://hub.docker.com/_/kibana/) image is being deprecated in favor of images maintained in Elastic's corporate repository.  The original official images were based on [openjdk](https://hub.docker.com/_/openjdk/).  I'm not entirely sure what the new images are based on, but they seem locked down and my custom scripts no longer work.

The `5.2.0` image here is still based on the original Kibana 5.2.0 image.  Later images are based directly on [openjdk](https://hub.docker.com/_/openjdk/) with Kibana downloaded and installed.  The new image layout is very similar to the original official image.

**NOTE:**

Elasticsearch, Kibana, and Metricbeat are designed to run together as a combined system.  You should deploy the same version of each component to your cluster and when it's time to upgrade, always upgrade the Elasticsearch cluster first, followed by the Metricbeat and Kibana.

# Description

[Kibana](https://www.elastic.co/guide/en/kibana/current/introduction.html) provides a dashboard interface over an Elasticsearch database.  This is typically deployed within a neonCLUSTER so that operators can examine events emitted by cluster nodes and services as well as monitor cluster status by analyzing the host and container information captured by Metricbeat.

# Configuration

You need to specify the following environment variables when running the container (these are referenced by the `elasticsearch.yam` configuration file):

* **ELASTICSEARCH_URL** - URL to the Elasticsearch cluster.

**NOTE**: This URL should not include a trailing "/".

Kibana listens internally on the default **port 5601**.

# Deployment

This image is typically deployed as a Docker service like:

````
docker service create \
    --name neon-log-kibana \
	--detach=false \
    --mode global \
    --restart-delay 10s \
    --endpoint-mode vip \
    --network neon-private \
    --constraint "node.role==manager" \
    --publish 5001:5601 \
    --mount type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true \
    --env ELASTICSEARCH_URL=http://neon-log-esdata.cluster:5303 \
    --log-driver json-file
````
