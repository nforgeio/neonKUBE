**Do not use: Work in progress**

# Supported Tags

* `1.0.0, latest`

# Description

The **neon-couchbase-manager** service manages the configuration and monitoring of a Couchbase database cluster deployed via a **neon-cli** `neon db create couchbase ... DBNAME` command.  This command deploys database nodes as individual containers and the management service with names that derive from the database name specified.  This command also persists information about the database to Consul.

For example, `neon db create couchbase ... mydatabase` will deploy:

* **neoncluster/neon-couchbase-manager** manager service named **neon-db-mydatabase-manager**.

* Couchbase node containers named **neon-db-mydatabase** using the **neoncluster/couchbase** image to each target node.

* Persist information about the cluster to Consul at `neon/database/mydatabase`

The **neon-db-** prefix and the **-manager** suffix are conventions NeonCluster uses to identify database services and their associated managers.

The current implementation of the Couchbase manager wakes up perodically and does the following:

1. Queries Consul for information about the cluster.

2. Joins each instance to the cluster if it isn't already a member.

3. Writes cluster status information to Consul at: `neon/database/DATABASENAME`

# Environment Variables

* **DATABASE** Is the base name of the Couchbase service being managed (e.g. `mydatabase`).

* **LOG_LEVEL** (*optional*) Specifies the logging level: `FATAL`, `ERROR`, `WARN`, `INFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Deployment

**IMPORTANT:** This service needs access to the Docker Swarm REST API and must be deployed only to cluster manager nodes.

**neon-consul-manager** service will be deployed automatically by **neon-cli** when a

&nbsp;&nbsp;&nbsp;&nbsp;`neon db create couchbase ... DBNAME`

command is executed.
