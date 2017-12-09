# Image Tags

The following image tags identify archived images which will not be deleted but are not maintained.

* `5.2.0`
* `5.3.0`
* `5.4.0`
* `5.5.0`

Supported images are tagged with the Metricbeat version plus the image build date.

**NOTE:**

Elasticsearch, Kibana, and Metricbeat are designed to run together as a combined system.  You should deploy the same version of each component to your cluster and when it's time to upgrade, always upgrade the Elasticsearch cluster first, followed by the Metricbeat and Kibana.

# Description

[Metricbeat](https://www.elastic.co/guide/en/beats/metricbeat/current/metricbeat-overview.html) is an agent that runs on each cluster node that captures and ships node and container metrics to the cluster Elasticsearch logs for analysis and viewing via Kibana.

By default, this container launches Metricbeat configured to capture the following node metrics:

* CPU and System Load
* Filesystem Statistics and Summaries
* Process Statistics
* Disk I/O
* Memory
* Network

You may may modify these behaviors by creating a derived image, modifying the `/metricbeat.yml.sh` configuration script and then redeploying the cluster's **neon-log-metricbeat** containers as described below.

NOTE: Although it would be nice to run Metricbeat as a Docker service, that doesn't work because it has to run on the host network.

# Environment Variables

* **ELASTICSEARCH_URL** (*optional*) - URL of the Elasticsearch cluster where the metrics are to be persisted.  The container will send these to the neonCLUSTER's Elasticsearch log storage cluster by default.

* **PERIOD** (*optional*) - interval at which metrics are collected with an "s" or "m" suffix for seconds or minutes.  This defaults to **60s**.

* **PROCESSES** (*optional*) - JSON array specifying the regex's of the process names for which statistics are to be gathered.  This defaults to **['dockerd','consul','vault']**.

* **LOG_LEVEL** (*optional*) - Metricbeat log level.  This may be one of *critical*, *error*, *warning*, *info*, or *debug*.  This defaults to **debug**.

# Deployment

**metricbeat** is deployed as a container to all cluster nodes.  The container expects some volumes to be mounted and must be run on the host network as explained [here](https://www.elastic.co/guide/en/beats/metricbeat/current/running-in-container.html).

You may also run this image with the `import-dashboards` argument.  This loads the metrics Kibana dashboards into the Elasticsearch log cluster.  

**neon-cli** handles **neon-log-metricbeat** container deployment and dashboard initialization when the cluster is provisioned using the following Docker commands:

````
# Deploy the container.

docker run \
    --name neon-log-metricbeat \
    --detatch \
    --net host \
    --restart always \
    --volume /etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    --volume /proc:/hostfs/proc:ro \
    --volume /:/hostfs:ro \
    --log-driver json-file \
    neoncluster/metricbeat

# Install the dashboards.
    
docker run --rm --name neon-log-metricbeat-dash \
    --volume=/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    neoncluster/metricbeat import-dashboards
````
&nbsp;
# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.

# Upgrading

To upgrade to the latest version of Metricbeat, run these commands on every cluster node:

````
docker pull neoncluster/metricbeat
docker rm neon-log-metricbeat

docker run \
    --name neon-log-metricbeat \
    --detatch \
    --net host \
    --restart always \
    --volume /etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    --volume /proc:/hostfs/proc:ro \
    --volume /:/hostfs:ro \
    --log-driver json-file \
    neoncluster/metricbeat
````
&nbsp;
and then run this command on a single node to upgrade the dashboards:
````
docker run --rm --name neon-log-metricbeat-dash \
    --volume=/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    neoncluster/metricbeat import-dashboards
````
&nbsp;
