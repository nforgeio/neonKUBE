# Description

The [nhive/varnish](https://hub.docker.com/r/nhive/varnish/) and [nhive/neon-proxy-cache](https://hub.docker.com/r/nhive/neon-proxy-cache/) images rely on a slightly customized build of **varnishd** to work correctly.  The issue centers around the inability of Docker services to mount a TMPFS that enables **exec**.  The custom build comments out a couple lines of initialization code.

# Environment Variables

This image is configured by the following environment variables:

* `GIT_REPO` (*required*) - Specifies the URL of the Git repository holding the modified Varnish source code.

* `GIT_BRANCH` (*required*) - Specifies the Git branch to be compiled.

* `PACKAGE_VERSION` (*required*) - Specifies the full version for the package including the revision (e.g. **6.1.1*).

* `TEST_BUILD` (*optional*) - Optionally runs the unit tests after the build when `TEST_BUILD=1`.  These may take 30+ minutes to run.

# Building Varnish

Building Varnish with this container is very easy.  All you need to do is specify the source repository URL as `GIT_REPO` and the build branch as `GIT_BRANCH` and then mount the host folder where you want the output binary to be written to `/mnt/output` and then run the container:

```
docker run \
    --rm \
    --env "GIT_REPO=https://github.com/jefflill/varnish-cache.git" \
    --env "GIT_BRANCH=6.1" \
    --env "PACKAGE_VERSION=6.1.1" \
    --env "TEST_BUILD=1" \
    --mount type=bind,src=%NF_BUILD%,dst=/mnt/output \
    nhive/varnish-builder
```
&nbsp;
Here are the steps the container will perform to build Varnish:

1. Make a local clone of the repository.
2. Switch to the specified branch.
3. Build Varnish.
4. Run unit tests if `TEST_BUILD` is defined.
5. ZIP the Varnish Cache binaries and other files into `varnish-6.1.zip`
6. Copy the ZIP file to `/mnt/output`.
