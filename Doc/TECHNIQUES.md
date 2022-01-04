# Developer Tips and Techniques

This document describes and links to various useful Kubernetes and other tips and techniques for deployment, debugging, etc.

## netshoot

This link discusses techniques for temporaily running a debug container within a target container's namespace (Docker) or adding 
a debug container to a pod.

https://hub.docker.com/r/nicolaka/netshoot

#### Key Takeaways

* The https://hub.docker.com/r/nicolaka/netshoot container imaage comes preinstalled with lots of debugging tools, like **tcpdump**...

* Provision an ephemerial container within an existing pod using **kubectl debug ...**: 

  This is discussed here: https://downey.io/blog/kubernetes-ephemeral-debug-container-tcpdump/

**Examples:**

Creates a new **netshoot** container named **debug** in POD and starts an interactive bash session:

`neon debug -it POD --container=debug --image=docker.io/r/nicolaka/netshoot -- /bin/bash`

#### ksniff

https://github.com/eldadru/ksniff

This looks interesting.  It claims to be able to forward network traffic from a pod network namespace to Wireshark running on your workstation.
