**Do not use: Work in progress**

Base Treasure Data TD-AGENT log event processor configured for NeonClusters.

# Supported Tags

* `2, latest`

# Configuration

This deploys a very basic (do nothing) TD-AGENT process intended to act as the base image for the agents deployed to a Neon Cloud cluster (e.g. the event forwarder deployed to each cluster node and the cluster log aggregator service.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
