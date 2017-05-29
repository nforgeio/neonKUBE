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
