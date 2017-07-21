**Do not use: Work in progress**

This image extends the official [Kibana repository]:(https://hub.docker.com/_/kibana/) by adding the **X-PACK** plugins.

# Supported Tags

The image tagging scheme mirrors that of the offical [Kibana repository]:(https://hub.docker.com/_/kibana/).

* `5.2.0, 5.0, 5, latest`

# Configuration

You need to specify the following environment variables when running the container (these are referenced by the `elasticsearch.yam` configuration file):

* **ELASTICSEARCH_URL** The URL to the Elasticsearch cluster.

**NOTE**: This URL should not include a trailing "/".

Kibana listens internally on the default **port 5601**.

# Deployment

This image is typically deployed as a Docker service like:

````
docker service create \
    --name neon-log-kibana \
    --mode global \
    --restart-delay 10s \
    --endpoint-mode vip \
    --network neon-cluster-private \
    --constraint "node.role==manager" \
    --publish 5001:5601 \
    --mount type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true \
    --env ELASTICSEARCH_URL=http://neon-log-esdata.cluster:5303 \
    --log-driver json-file
````
