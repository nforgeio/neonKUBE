**DO NOT USE: Work in progress**

# Supported Tags

* `2, latest`

# Configuration

This is the base Treasure Data TD-AGENT log event processor that will be referenced by other images implementing the neonCLUSTER log event pipeline.

This deploys a very basic (do nothing) TD-AGENT process intended to act as the base image for the agents deployed to a Neon Cloud cluster (e.g. the event forwarder deployed to each cluster node and the cluster log aggregator service.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
