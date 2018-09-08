# Image Tags

The most recent **Couchbase Community Edition** build will be tagged as `latest`.  Couchbase **Enterprise** and other Community builds are also available.

Images are tagged with the Git branch, image build date, and Git commit and an optional `-dirty` suffix if the image was built from a branch with uncommitted changes or untracked files.

From time-to-time you may see images tagged like `:BRANCH-*` where *BRANCH* identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

**DO NOT USE FOR PRODUCTION**

This image is intended for development and testing purposes.  It provisions a single empty bucket.  Here are the default settings:

Admin Username: **Administrator**
Admin Password: **password**
Bucket: **test**

You can override these settings and other settings by passing one or more of the following environment variables:

* `CLUSTER_NAME` (*optional*) - cluster name (defaults to `test`

* `USERNAME` (*optional*) - cluster administrator username (defaults to `Administrator`

* `PASSWORD` (*optional*) - cluster administrator password (defaults to `password`

* `BUCKET_NAME` (*optional*) - the bucket name (defaults to `test`

* `CLUSTER_RAM_MB` (*optional*) - data services RAM quota in MB (defaults to `256`

* `FTS_RAM_MB` (*optional*) - full-text indexing service RAM quota in MB (defaults to `256`

* `INDEX_RAM_MB` (*optional*) - index service RAM quota in MB (defaults to `256`

* `BUCKET_RAM_MB` (*optional*) - bucket cache RAM in mMB (defaults to `256`

# Deployment

This image is intended to be deployed as a Docker container on the local machine using the following command.  Note that the container name below is [cb-test] but this can be customized as necessary.

````
docker run --detach \
    --name cb-test \
    -p 8091-8094:8091-8094 \
    -p 11210:11210 \
    nhive/couchbase-test
````
&nbsp;
