# Image Tags

These base images are tagged with the TD-Agent version plus the image build date.  The most recent build will be tagged as **latest**.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Configuration

This is the base Treasure Data TD-AGENT log event processor that will be referenced by other images implementing the neonCLUSTER log event pipeline.

This deploys a very basic (do nothing) TD-AGENT process intended to act as the base image for the agents deployed to a Neon Cloud cluster (e.g. the event forwarder deployed to each cluster node and the cluster log aggregator service.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
