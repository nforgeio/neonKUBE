# Image Tags

These base images are tagged with the TD-Agent version plus the image build date.  The most recent build will be tagged as `latest`.

# Configuration

This is the base Treasure Data TD-AGENT log event processor that will be referenced by other images implementing the neonHIVE log event pipeline.

This deploys a very basic (do nothing) TD-AGENT process intended to act as the base image for the agents deployed to a Neon hive (e.g. the event forwarder deployed to each hive node and the hive log aggregator service.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
