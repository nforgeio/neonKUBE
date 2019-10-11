# Image Tags

These images are tagged with the Neon build version.

# Description

This implements the Kubernetes operator that handles neonKUBE cluster setup and management.  This is deployed automatically during cluster setup.

# Environment Variables

* `CADENCE_CLUSTER` (*required*) - Comma separated HTTP/HTTPS URIs to one or more Cadence cluster servers.
* `CADENCE-DOMAIN` (*required*) - Specifies the Cadence domain where the workflows will be registered.
* `CADENCE-TASKLIST` (*required*) - Specifies the Cadence task list for the registered workflows.
* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `DEBUG`, or `NONE` (defaults to `INFO`).
