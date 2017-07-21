**Do not use: Work in progress**

# Supported Tags

The image tagging scheme mirrors the Elastic Metricbeat [releases](https://www.elastic.co/downloads/past-releases).

* `5.2.0, 5.0, 5, latest`

# Description

This image is used to deploy containers named **neon-log-metricbeat** to each node in a neonCLUSTER.  This deploys an [Elastic Metricbeat](https://www.elastic.co/guide/en/beats/metricbeat/current/metricbeat-overview.html) that captures and ships Docker node metrics to the cluster's Elasticsearch logs for analysis and viewing via Kibana.

By default, this container launches Metricbeat configured to capture the following node metrics:

* CPU and System Load
* Filesystem Statistics and Summaries
* Process Statistics
* Disk I/O
* Memory
* Network

You may may modify these behaviors by creating a derived image, modifying the `/metricbeat.yml.sh` configuration script and then redeploying the cluster's **neon-log-metricbeat** containers as described below.

# Environment Variables

* **ELASTICSEARCH_URL** (*optional*) is URL of the Elasticsearch cluster where the metrics are to be persisted.  The container will send these to the neonCLUSTER's Elasticsearch log storage cluster by default.

* **PERIOD** (*optional*) is the interval at which metrics are collected with an "s" or "m" suffix for seconds or minutes.  This defaults to **60s**.

* **PROCESSES** (*optional*) is a JSON array specifying the regex's of the process names for which statistics are to be gathered.  This defaults to **['dockerd','consul','vault']**.

* **LOG_LEVEL** (*optional*) specifies the Metricbeat log level.  This may be one of *critical*, *error*, *warning*, *info*, or *debug*.  This defaults to **debug**.

# Deployment

**metricbeat** is deployed as a container to all cluster nodes.  This container simply launches Metricbeat with the appropriate arguments.  The container expects some volumes to be mounted and must be run on the host network as explained [here](https://www.elastic.co/guide/en/beats/metricbeat/current/running-in-container.html).

You may also run this image with the `import-dashboards` argument.  This loads the metrics Kibana dashboards into the Elasticsearch log cluster.  

**neon-cli** handles **neon-log-metricbeat** container deployment and dashboard initialization when the cluster is provisioned using the following Docker commands:

````
docker run \
    --name neon-log-metricbeat \
    --detach \
    --restart always \
    --volume=/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    --volume=/proc:/hostfs/proc:ro \
    --volume=/:/hostfs:ro \
    --net=host \
    neoncluster/metricbeat
    
docker run --rm --name neon-log-metricbeat-dash-init \
    --volume=/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    neoncluster/metricbeat import-dashboards
````
&nbsp;
# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.

# Upgrading

To upgrade to the latest version of Metricbeat, you'll need to use the `nc exec` command to run the following script on each cluster node:

````
docker rm -f neon-log-metricbeat

docker pull neoncluster/metricbeat

docker run --detach --name neon-log-metricbeat \
    --volume=/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    --volume=/proc:/hostfs/proc:ro \
    --volume=/:/hostfs:ro \
    --net=host \
    neoncluster/metricbeat
````
&nbsp;
and then run this command on a single node to upgrade the dashboards:
````
docker run --rm --name neon-log-metricbeat-dash-init \
    --volume=/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro \
    neoncluster/metricbeat import-dashboards
````
&nbsp;
