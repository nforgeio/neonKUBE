Base neonHIVE **node.js** image.

# Image Tags

Images are tagged with the Git branch, image build date, and Git commit.  The most recent production build will be tagged as `latest`.

# Configuration

This base image deploys a simple service that listens on `port 80` and *"Hello World!"* to the output.

You may also specify a custom output string by passing one as the `OUTPUT` environment variable.  This can be useful for setting up simple services to test HAProxy or Nginx configurations.

Derived images can overwrite the `/program.js` and `/docker-entry-point.sh` files as necessary.
