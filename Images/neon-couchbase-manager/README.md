**Do not use: Work in progress**

# Supported Tags

* `1.0.0, latest`

# Description

The **neon-couchbase-manager** service manages the configuration and monitoring of a Couchbase database cluster deployed via a **neon-cli** `neon db create couchbase ... NAME` command.  This command actually deploys two services named after the database.

For example, `neon db create couchbase ... mydatabase` will deploy:

* A Couchbase database service using the **neoncluster/couchbase** image named **neon-db-mydatabase**

* A **neoncluster/neon-couchbase-manager** manager service named **neon-db-mydatabase-manager**.

The **neon-db-** prefix and the **-manager** suffix are conventions NeonCluster uses to identify database services and their associated managers.

The current implementation of the Couchbase manager wakes up perodically and does the following:

1. Queries the Docker swarm for information about the associated Couchbase service including the virtual IP addresses of the instances.

2. Joins each instance to the cluster if it isn't already a member.

3. Writes cluster status information to Consul at: `neon/database/neon-db-DATABASENAME`

# Environment Variables

* **DATABASE_SERVICE** Is the name of the Couchbase service being managed.

* **NODES** Is the comma separated list of node names where the Couchbase instances are deployed.

* **LOG_LEVEL** (*optional*) Specifies the logging level: `FATAL`, `ERROR`, `WARN`, `INFO`, `DEBUG`, or `NONE` (defaults to `INFO`).

# Deployment

**IMPORTANT:** This service needs access to the Docker Swarm and must be deployed only to cluster manager nodes.

**neon-consul-manager** service will be deployed automatically by **neon-cli** when a

&nbsp;&nbsp;&nbsp;&nbsp;`neon db create couchbase ... NAME`

command is executed.
