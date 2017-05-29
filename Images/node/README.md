**Do not use: Work in progress**

Base NeonCluster **node.js** image.

# Supported Tags

* `latest`

# Configuration

This base image deploys a simple service that listens on **port 80**.

By default, it simply writes **"Hello World!"** to the output.  You may also specify a custom output string by passing one as the **OUTPUT** environment variable.  This can be useful for setting up simple services to test HAProxy or Nginx configurations.

Derived images can overwrite the `/program.js` and `/docker-entry-point.sh` files as necessary.
