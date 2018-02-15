# neon-volume Docker plugin

This Docker volume plugin is intended for use by neonCLUSTERs to easily bind containers and services to volumes hosted on the cluster's **CephFS** distributed file system (if this was enabled when the cluster was deployed).

# CephFS and neonCLUSTER

neonCLUSTER can be provisioned to with a the [Ceph Storage Cluster](http://ceph.com).  This is an enterprise grade distributed storage solution that provides for distributing storage across multiple services.  At the base level, this is a distributed object store.  Ceph builds on this to create a POSIX compatible file system called **CephFS**.

When a neonCLUSTER is deployed with Ceph enabled, CephFS is also enabled such that the distributed file system is mounted on all cluster hosts (managers, workers, and pets) at `/cfs` (read as *CephFS*, *ClusterFS*, or just *CFS*).

CFS is organized as follows:

&nbsp;&nbsp;&nbsp;&nbsp;`\cfs\READY`
&nbsp;&nbsp;&nbsp;&nbsp;`\cfs\docker\`
&nbsp;&nbsp;&nbsp;&nbsp;`\cfs\neon\`

`/cfs/READY` is a zero byte file written to the file system root.  This is used by the `neon-volume` plugin to detect when the volume is mounted and ready.  You must leave this file alone.

`/cfs/docker` is where `neon-volume` manages volumes to be associated with Docker containers and services.  You'll generally want to leave this folder alone to allow `neon-volume` to do its thing.

`/cfs/neon` is reserved for neonCLUSTER related Docker containers and services as well as cluster host services.  You should leave this folder alone as well.

In general, the best practice is to limit access to Docker containers and services using `neon-volume` to manage the volumes.  For circumstances where custom host-level services need a shared file system, you should take care to create a custom directory with a name that'll be unlikely to conflict with other future uses.

# Attribution

This project was adapted from the Cameron Spear's **local-persist** Docker volume plugin at:

	https://github.com/CWSpear/local-persist

Copyright (c) 2016 Cameron Spear
Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
