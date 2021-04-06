# Image Tags

This image is persisted to the registry with two tags: `0` and `latest`

# Description

This container really does nothing except for sleep.  It's presence in a cluster as a **neon-kubefixture** deployment is used by the `Neon.Kube.Xunit.KubernetesFixture` to determine that it's safe to run unit tests in the cluster.

This is used as a failsafe mechanism to avoid running unit tests on production clusters.

