# Description

The [nhive/varnish](https://hub.docker.com/r/nhive/varnish/) and [nhive/neon-proxy-cache](https://hub.docker.com/r/nhive/neon-proxy-cache/) images rely on a slightly customized build of **varnishd** to work correctly.  The issue centers around the inability of Docker services to mount a TMPFS that enables **exec**.  The custom build comments out a couple lines of initialization code.

# Environment Variables

This image is configured by the following environment variables:

* `GIT_VARNISH_REPO` (*required*) - Specifies the URL of the Git repository holding the modified Varnish source code.

* `GIT_VARNISH_BRANCH` (*required*) - Specifies the Varnish Git branch to be compiled.

* `GIT_VMOD_DYNAMIC_REPO` (*required*) - Specifies the URL of the Git repository holding the **libvmod-dynamic** source code.

* `GIT_VMOD_DYNAMIC_BRANCH` (*required*) - Specifies the **libvmod-dynamic** Git branch to be compiled.

* `TEST_BUILD` (*optional*) - Optionally runs the unit tests after the build when `TEST_BUILD=1`.  These may take 30+ minutes to run.

# Building Varnish

Building Varnish with this container is very easy.  All you need to do is specify the source repositories and then mount the host folder where you want the output binary to be written to `/mnt/output` and then run the container like:

```
docker run \
    --rm \
    --env "GIT_VARNISH_REPO=https://github.com/jefflill/varnish-cache.git" \
    --env "GIT_VARNISH_BRANCH=6.1" \
    --env "GIT_VMOD_DYNAMIC_REPO=https://github.com/jefflill/libvmod-dynamic" \
    --env "GIT_VMOD_DYNAMIC_BRANCH=master" \
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
