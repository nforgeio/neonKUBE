# Image Tags

This image is persisted to the registry with two tags: `0` and `latest`

# Description

This is a small Alpine based container image used to synchronize the Docker clock with
that of the host machine.  This can be a problem for Docker running in WSL2 mode due to
problems with the WSL2 kernel:

    https://github.com/nforgeio/neonKUBE/issues/1166

neonFORGE currently runs this container before the `DockerComposeFixture` starts an application
to synchronize the clocks.  The container needs to run as **privileged** to be able to access
the Docker host clock:

```
docker run --privileged alpine
```
