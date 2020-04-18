# Image Tags

These images are tagged with the Neon build version.

# Description

This implements the Kubernetes operator that handles neonKUBE cluster setup and management.  This is deployed automatically during cluster setup.

# Environment Variables

* `CADENCE_SERVERS` (*required*) - Comma separated Cadence URIs (i.e. `cadence://10.0.0.2:7933`) to one or more Cadence cluster servers.
* `CADENCE-DOMAIN` (*required*) - Specifies the Cadence domain where the workflows will be registered.
* `CADENCE-TASKLIST` (*required*) - Specifies the Cadence task list for the registered workflows.
* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `TRANSIENT`, `DEBUG`, or `NONE` (defaults to `INFO`).
