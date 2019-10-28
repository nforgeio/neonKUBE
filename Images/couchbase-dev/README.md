# Image Tags

The most recent **Couchbase Community Edition** build will be tagged as `latest`.  Couchbase **Enterprise** and other Community builds are also available.

These images are tagged with the Git branch, image build date, and Git commit.

# Description

**DO NOT USE FOR PRODUCTION**

This image is intended for development and testing purposes only.  It provisions a single empty bucket.  Here are the default settings:

Admin Username: **Administrator**
Admin Password: **password**
Bucket: **test**

You can override these settings and other settings by passing one or more of the following environment variables:

* `CLUSTER_NAME` (*optional*) - cluster name (defaults to `test`

* `USERNAME` (*optional*) - cluster administrator username (defaults to `Administrator`)

* `PASSWORD` (*optional*) - cluster administrator password (defaults to `password`)

* `BUCKET_NAME` (*optional*) - the bucket name (defaults to `test`

* `CLUSTER_RAM_MB` (*optional*) - data services RAM quota in MB (defaults to `256`)

* `FTS_RAM_MB` (*optional*) - full-text indexing service RAM quota in MB (defaults to `256`)

* `INDEX_RAM_MB` (*optional*) - index service RAM quota in MB (defaults to `256`)

* `BUCKET_RAM_MB` (*optional*) - bucket cache RAM in mMB (defaults to `256`)

# Deployment

This image is intended to be deployed as a Docker container on the local machine using the following command.  Note that the container name below is [cb-test] but this can be customized as necessary and also that this example publishes all public Couchbase ports.  You may be able to get by with fewer ports depending on your scenario.

````
docker run --detach \
    --name cb-test \
    --mount type=volume,target=/opt/couchbase/var \
    -p 4369:4369 \
    -p 8091-8096:8091-8096 \
    -p 9100-9105:9100-9105 \
    -p 9110-9118:9110-9118 \
    -p 9120-9122:9120-9122 \
    -p 9999:9999 \
    -p 11207:11207 \
    -p 11209-11211:11209-11211 \
    -p 18091-18096:18091-18096 \
    -p 21100-21299:21100-21299 \
    nkubeio/couchbase-dev
````
&nbsp;
