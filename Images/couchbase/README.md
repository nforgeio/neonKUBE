**Do not use: Work in progress**

# Supported Tags

* `4.5.1, latest`

# Description

The **neon-couchbase** is used to deploy Couchbase Community Edition database clusters to a NeonCluster using the **neon-cli**.  The idea here is that being a persisted database, operators will need to give Couchbase clusters more TLC than they would stateless services.  **neon-cli** provides tools that help an operator treat the cluster service instances and data volumes more like pets than cattle.

This is a custom built image that includes Couchbase Community Edition Server:

* Image version matches the Couchbase server version  
* Logs are written to standard output to be available to standard Docker logging
* **neon-cli** a **neoncluster/neon-couchbase-manager** service that cluster formation and monitoring

# Deployment

NeonCluster hosts support only a single Couchbase instances.  Any attempt to host multiple instances will result in port conflicts.  This isn't too unreasonable for production databases and it's possible that we'll relax this constraint in the future.

This image is generally deployed using the following **neon-cli** command:

&nbsp;&nbsp;&nbsp;&nbsp;`neon db create couchbase ...`

It may also be deployed manually.  Here are some things to keep in mind.

**ULIMITs for Production Deployments**

You'll need to increase the ULIMIT settings for the container to support running under load.  Docker doesnt currently support `unlimited` so you should use a value that's greater than available RAM (e.g. the 100GB values below):

&nbsp;&nbsp;&nbsp;&nbsp;`--ulimit nofile=40960:40960`
&nbsp;&nbsp;&nbsp;&nbsp;`--ulimit core=100000000:100000000`
&nbsp;&nbsp;&nbsp;&nbsp;`--ulimit memlock=100000000:100000000`

**Networks and Ports** 

The container must be deployed on the `--net=host` network and you must publish the following ports to the host network:

&nbsp;&nbsp;&nbsp;&nbsp;`8091-8094`
&nbsp;&nbsp;&nbsp;&nbsp;`11210`  

**Data Volume**

You'll want to have Couchbase persist its data to a named volume on the host for two reasons:

* The Docker container file system is not optimized for databases and will perform poorly.

* Named volumes will make it much easier to manage important data including preventing it from being deleted when the container is deleted (e.g. for an upgrade).

Couchbase expects a data volume to be mounted to:

&nbsp;&nbsp;&nbsp;&nbsp;`/opt/couchbase/var`
