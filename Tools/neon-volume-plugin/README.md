# neon-volume Docker plugin

This Docker volume plugin is intended for use by neonHIVEs to easily bind containers and services to volumes hosted on the hive's **CephFS** distributed file system (if this was enabled when the hive was deployed).

# CephFS and neonHIVE

neonHIVE can be provisioned to with a the [Ceph Storage Cluster](http://ceph.com).  This is an enterprise grade distributed storage solution that provides for distributing storage across multiple services.  At the base level, this is a distributed object store.  Ceph builds on this to create a POSIX compatible file system called **CephFS**.

When a neonHIVE is deployed with Ceph enabled, CephFS is also enabled such that the distributed file system is mounted on all hive hosts (managers, workers, and pets) at `/mnt/hivefs`.

hiveFS is organized as follows:

&nbsp;&nbsp;&nbsp;&nbsp;`\mnt/hivefs\READY`
&nbsp;&nbsp;&nbsp;&nbsp;`\mnt/hivefs\docker\`
&nbsp;&nbsp;&nbsp;&nbsp;`\mnt/hivefs\neon\`

`/mnt/hivefs/READY` is a zero byte file written to the file system root.  This is used by the `neon-volume` plugin to detect when the volume is mounted and ready.  You must leave this file alone.

`/mnt/hivefs/docker` is where `neon-volume` manages volumes to be associated with Docker containers and services.  You'll generally want to leave this folder alone to allow `neon-volume` to do its thing.

`/mnt/hivefs/neon` is reserved for neonHIVE related Docker containers and services as well as hive host services.  You should leave this folder alone as well.

In general, the best practice is to limit access to Docker containers and services using `neon-volume` to manage the volumes.  For circumstances where custom host-level services need a shared file system, you should take care to create a custom directory with a name that'll be unlikely to conflict with other future uses.
