Base Kong API Gateway (Alpine) image deployed by neonHIVE.

# Image Tags

Supported images are tagged with the Kong version plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

Kong (Alpine) image that adds REST API gateway functionality to the cluster.  This is deployed and configured (using environment variables) automatically during neonHIVE setup.

See [Kong API Gateway](http://konghq.com) for more information.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
