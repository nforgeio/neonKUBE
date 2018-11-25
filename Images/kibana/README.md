# Supported Tags

Supported are be tagged with the Kibana version plus the image build date.

**NOTE:**

Elasticsearch, Kibana, and Metricbeat are designed to run together as a combined system.  You should deploy the same version of each component to your cluster and when it's time to upgrade, always upgrade the Elasticsearch cluster first, followed by the Metricbeat and Kibana.

# Description

[Kibana](https://www.elastic.co/guide/en/kibana/current/introduction.html) provides a dashboard interface over an Elasticsearch database.  This is typically deployed within a neonHIVE so that operators can examine events emitted by cluster nodes and services as well as monitor cluster status by analyzing the host and container information captured by Metricbeat.

# Configuration

You need to specify the following environment variables when running the container (these are referenced by the `elasticsearch.yam` configuration file):

* `ELASTICSEARCH_URL` - URL to the Elasticsearch cluster.

**NOTE**: This URL should not include a trailing "/".

Kibana listens internally on the default `port 5601`

# Deployment

This image is typically deployed as a Docker service like (where *HIVENAME* is the hive's name):

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
    --mount type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true \
    --env ELASTICSEARCH_URL=http://neon-log-esdata.HIVENAME.nhive.io:5303 \
    --log-driver json-file
````
&nbsp;
You can also run this as a container to get JSON formatted information about the Kibana package:
````
docker run --rm nhive/kibana version
````
&nbsp;
This will return something like:
````
{
  "name": "kibana",
  "description": "Kibana is an open source (Apache Licensed), browser based analytics and search dashboard for Elasticsearch. Kibana is a snap to setup and start using. Kibana strives to be easy to get started with, while also being flexible and powerful, just like Elasticsearch.",
  "keywords": [
    "kibana",
    "elasticsearch",
    "logstash",
    "analytics",
    "visualizations",
    "dashboards",
    "dashboarding"
  ],
  "version": "6.1.1",
  "branch": "6.x",
  "build": {
    "number": 16350,
    "sha": "80e60a0f288696992b1874212ab4c41c9149901e"
  },
  "repository": {
    "type": "git",
    "url": "https://github.com/elastic/kibana.git"
  },
  "engines": {
    "node": "6.12.2"
  }
}
````
