# Image Tags

These images are tagged with the Neon build version.

# Description

This implements a Cadence workflow service used for **Neon.Cadence** unit testing.

# Environment Variables

* `CADENCE_SERVERS` (*required*) - Comma separated Cadence URIs (i.e. `cadence://10.0.0.2:7933`) to one or more Cadence cluster servers.
* `CADENCE_DOMAIN` (*required*) - Specifies the Cadence domain where the test workflows will be registered.
* `CADENCE_TASKLIST` (*required*) - Specifies the Cadence task list for the registered workflows.
* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `TRANSIENT`, `DEBUG`, or `NONE` (defaults to `INFO`).
