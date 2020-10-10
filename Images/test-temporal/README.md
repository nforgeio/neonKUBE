# Image Tags

These images are tagged with the Neon build version.

# Description

This implements a Temporal workflow service used for **Neon.Temporal** unit testing.

# Environment Variables

* `TEMPORAL_HOSTPORT` (*required*) - The Temporal service endpoint specified as **HOST:PORT** or **dns:///HOST:PORT**.
* `TEMPORAL_NAMESPACE` (*required*) - Specifies the Temporal namespace where the test workflows will be registered.
* `TEMPORAL_TASKQUEUE` (*required*) - Specifies the Temporal task queue for the registered workflows.
* `LOG_LEVEL` (*optional*) - logging level: `CRITICAL`, `SERROR`, `ERROR`, `WARN`, `INFO`, `SINFO`, `TRANSIENT`, `DEBUG`, or `NONE` (defaults to `INFO`).
