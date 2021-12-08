# Description

This container is intended to be deployed to Kubernetes for the purpose of manually playing
around from the inside of a cluster.  This image is based on Alpine and includes the several
handy utilities:

* bash 
* nano 
* curl 
* net-tools 
* tcpdump

The image entrypoint just sleeps for a year.  The idea is that you'll deploy this as a pod
and then exec into it manually via something like:

```
neon exec --stdin --tty PODNAME -- /bin/bash
```
