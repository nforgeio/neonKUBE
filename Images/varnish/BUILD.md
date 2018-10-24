# IMPORTANT: Build Notes

This image requires a slightly customized `varnishd` build.  The forked source code for this resides at https://github.com/jefflill/varnish-cache/ and modifies the startup behavior if the `VARNISH_HACK` environment variable is defined such that no attempts will be made to clear or create the shared memory log folder.  This folder should already exist and be empty for this scenario.

The custom [varnishd] binaries should be packaged as ZIP archives and must be manually build and uploaded to Amazon S3 at:

    https://s3.console.aws.amazon.com/s3/buckets/neoncluster/varnish-VERSION.zip

where **VERSION** is the Varnish release version (like **6.1.0**).

You'll need to manually perform the following steps to build and publish the `varnishd` binary before building the **neon-proxy-cache** and **varnish** images:

1. Build `varnishd` via the command below, specifying the Varnish branch/version:

   `docker run --rm --env "GIT_REPO=https://github.com/jefflill/varnish-cache.git" --env "GIT_BRANCH=6.1" --mount type=bind,src=%NF_BUILD%,dst=/mnt/output nhive/varnish-builder`

   It's a good idea to verify the build by adding the `--env CHECK=1` environment variable.  This will take 30+ minutes:

   `docker run --rm --env "GIT_REPO=https://github.com/jefflill/varnish-cache.git" --env "GIT_BRANCH=6.1" --env "CHECK=1" --mount type=bind,src=%NF_BUILD%,dst=/mnt/output nhive/varnish-builder`

2. Upload the ZIP file to S3 at:  https://s3.console.aws.amazon.com/s3/buckets/neoncluster

3. Make the S3 file **public**.
