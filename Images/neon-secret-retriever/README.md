# Image Tags

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

The most recent production build will be tagged as `latest`.

# Description

This image is intended to be deployed as a service with a Docker secret passed to it.  All the service does is read the secret and then persist it to a Consul key and then sleep until the service is removed.

This is useful for situations where we need to obtain a Docker secret outside of a Docker service.  One example is the `HiveFixture` Xunit test fixture.  It needs to HiveMQ settings so it can reset the HiveMQ cluster.

# So, what about security?

This might seem like a huge security hole, and perhaps it is.  This service is a quick and easy way to extract a secret from Docker and then save it someplace where it can be retrieved.  Seems pretty bad.

But here's the thing: any Docker image deployed as a service can do this, or even worse.  It might be possible to hack the [microsoft/aspnetcore](https://hub.docker.com/r/microsoft/aspnetcore/) image by adding a program that scanned for mounted Docker secrets to be posted somewhere on the Internet.  This might happen to any Docker image.  This seems pretty bad too.

Docker is essentially banking on the **root account** on the swarm managers being secure and that the person wielding the account is trustworthy.

So in my opinion, the availability or use of this image doesn't make a Docker cluster any less secure.

# Usage

The service requires:

* files and folders to be mounted such that the service application will be able to establish a Consul connection
* secret name
* two arguments: the desired secret and the target Consul key

Here's an example that writes the Docker secret named *MYSECRET* to Consul at *MYCONSULKEY*:

````
docker service create \
    --name secret-retriever \
    --detach=false \
    --mount type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true \
    --mount type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true \
    --mount type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true \
    --health-start-period 1s \
    --secret MYSECRET \
    nhive/neon-secret-retriever MYSECRET MYCONSULKEY

sleep 5
docker service rm secret-retriever
````
&nbsp;
This example starts the service passing the Docker secret and the Consul key path and then waits 5 seconds to be sure the service had a chance to perform the capture.  Then the service is removed.

Sleeping for 5 seconds is a bit clumsy.  A more advanced script or code could start the service and then poll Consul until it sees the secret written there.  Then it removes the service along with the Consul key.
