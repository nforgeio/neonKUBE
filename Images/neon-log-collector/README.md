# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`

# Description

This image is deployed as the `neon-log-collector` service.  This service acts handles the transformation and aggregation of log events captured on each hive node by the `neon-log-host` containers running on the nodes.  This image relies on the [Fluentd](http://www.fluentd.org/) TD-Agent agent to handle the actual transformation and transmission of the logs.

This product includes the GeoLite2 database created by MaxMind, available from [http://www.maxmind.com](http://www.maxmind.com).  MaxMind releases an updated database once a month, around the first Tuesday of the month

The `neon-log-collector` service is responsible for receiving events from the hosts and then:

* Filtering out undesired events.
* Adding standard datacenter, hive and node events.
* Identifying standard fields such as timestamp, log level, service, service type, module, activity, container,...
* Parsing TCP and HTTP traffic through hive proxies including Browser and GeoIP lookups.
* Parsing events from known applications.

The `neon-log-host` image is deployed as local containers on every hive node (both managers and workers) to perform these functions:

* Capturing local systemd journal events.
* Capturing local syslog events.
* Receiving container events forwarded by the local Docker daemon via the `fluent` log driver.
* Forwarding events on to the `neon-log-collector` service.

# Included TD-Agent plugins

* [elasticsearch (output)](https://github.com/uken/fluent-plugin-elasticsearch) This plug-in is used to persist log events to an Elasticsearch cluster using the Logstash format.  This allows Kabana to be used for analysis.

* [record-modifier (filter)](https://github.com/repeatedly/fluent-plugin-record-modifier) This plug-in provides some extended record manipulation capabilities.

* Custom neonHIVE filter plugins: `neon-docker` `neon-logfields`, `neon-loglevel`, `neon-proxy`, `neon-proxy-geoip`, and `neon-timestamp`.

# Configuration

You need to specify the following environment variables when running the container (these are referenced by the `elasticsearch.yam` configuration file):

* `ELASTICSEARCH_HOST` - (*required*) Hostname used to reach the Elasticsearch cluster.
* `ELASTICSEARCH_PORT` - (*required*) Port used to reach the Elasticsearch cluster.

# Deployment

This service is deployed automatically when the `neon-cli` configures a hive using the following command:

````
docker service create \
    --name neon-log-collector \
    --detach=false \
    --mode global \
    --restart-delay 10s \
    --endpoint-mode vip \
    --network neon-private \
    --constraint node.role==manager \
    --mount type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true \
    --env SHARD_COUNT=5 \
    --env REPLICA_COUNT=0 \
    --log-driver json-file \
    nhive/neon-log-collector
````
&nbsp;
# Extending or Replacing this Image

You may find it necessary to modify or replace the behavior of the `neon-log-collector` service.  Some common scenarios are:

* Handling a new log format.
* Persisting for forwarding events somewhere else (e.g. Splunk).
* Performing event real-time analysis.
* Upgrading to a premium MaxMind database.

**Install Standard Fluentd Plugins**

The Fluentd community has a a lot of plugins available.  You can browse the full list [here](http://www.fluentd.org/plugins).  These are typically publshed as Ruby GEMs and can be installed using a command like:

&nbsp;&nbsp;&nbsp;&nbsp;`/usr/sbin/td-agent-gem install PLUGIN --no-document`

**Install Custom Fluentd Plugins**

It is often necessary to implement your own custom plugins in Ruby.  Install these by copying your `PLUGIN.rb` Ruby script to the image's `/etc/td-agent/plugin/` directory.

**Modify the TD-Agent Configuration**

You'll probably need to modify the configuration file located here: `/etc/td-agent/td-agent.conf`

**Installing a Premium MaxMind Database**

The `neon-log-collector` includes the free [MaxMind.com](http://maxmind.com) GeoLite2-City database.  This is used to map client IP addresses logged for network traffic captured by the neonHIVE proxies into geographical location information, including the latitute, longitude, continent, country, and city.  This database is provided under the [Creative Commons License](https://creativecommons.org/licenses/by-sa/4.0/) and is a reasonable first start for many hives.

MaxMind licenses much more accurate databases at a very reasonable cost, so you may wish to upgrade to one of their premium products.  To do this, you first the [MaxMind.com](http://maxmind.com) site to purchase your license.  Then modify your new image's Dockerfile to add your compressed database here:

&nbsp;&nbsp;&nbsp;&nbsp;`/geoip/database//geoip/database.mmdb.gz`

Note that the database file must be named `database.mmdb.gz`.  The base `neon-log-collector` image will decompress the database when it starts.

**Updating Your Hive**

For new hives, you can specify the new collector image in the hive definition before deployment and for existing hives you can simply use this command to deploy the new image:

&nbsp;&nbsp;&nbsp;&nbsp;`nc docker service update --image YOUR-IMAGE neon-log-collector`
