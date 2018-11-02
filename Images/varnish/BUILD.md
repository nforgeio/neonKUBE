# IMPORTANT: Build Notes

This image requires a slightly customized `varnishd` build.  The forked source code for this resides at https://github.com/jefflill/varnish-cache/ and modifies the startup behavior if the `VARNISH_HACK` environment variable is defined such that no attempts will be made to clear or create the shared memory log folder.  This folder should already exist and be empty for this scenario.

The custom [varnishd] binaries should be packaged as ZIP archives and must be manually build and uploaded to Amazon S3 at:

    https://s3.console.aws.amazon.com/s3/buckets/neoncluster/varnish-VERSION.zip

where **VERSION** is the Varnish release version (like **6.1.0**).

You'll need to manually perform the following steps to build and publish the `varnishd` binary before building the **neon-proxy-cache** and **varnish** images:

1. If you're using a forked Varnish GitHub repository, be sure to sync it with the origin as required.

2. You'll need to obtain the full package version number for the Varnish package being created:

   * Start an Ubuntu-16.04 container.
   * Run: `apt-get update`
   * Run this (replacing the **61** with the desired family:
   
   `curl -s https://packagecloud.io/install/repositories/varnishcache/varnish61/script.deb.sh | bash`

   * Run: `apt-cache madison varnish`
   * Make a note of the currently published versions.  You can choose one of these.

3. Build `varnishd` via the command below, specifying the Varnish branch/version:

   `docker run -it --rm --env "GIT_REPO=https://github.com/jefflill/varnish-cache.git" --env "GIT_BRANCH=6.1" --mount type=bind,src=%NF_BUILD%,dst=/mnt/output nhive/varnish-builder`

   It's a good idea to verify the build for each Varnish version at least once by adding the `--env TEST_BUILD=1` environment variable.  This will take 30+ minutes:

   `docker run -it --rm --env "GIT_REPO=https://github.com/jefflill/varnish-cache.git" --env "GIT_BRANCH=6.1" --env "TEST_BUILD=1" --mount type=bind,src=%NF_BUILD%,dst=/mnt/output nhive/varnish-builder`

   **NOTE:** It appears that the **6.0.x* builds always perform the unit tests.

4. Upload the ZIP file to S3 at:  https://s3.console.aws.amazon.com/s3/buckets/neoncluster

5. Make the S3 file **public**.
