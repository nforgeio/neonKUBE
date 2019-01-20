#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-kubernetes.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script handles the installation of the base Kubernetes binaries on a node.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-KUBERNETES                         **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup kubernetes

# We're using specific package versions identified by the neonKUBE headend service.

curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
echo "deb https://apt.kubernetes.io/ kubernetes-xenial main" > /etc/apt/sources.list.d/kubernetes.list
safe-apt-get update
safe-apt-get install -yq kubeadm=$<neon.kube.kubeadm.package_version>
safe-apt-get install -yq kubectl=$<neon.kube.kubectl.package_version>
safe-apt-get install -yq kubelet=$<neon.kube.kubelet.package_version>

# We need to pin the package versions because updating is not seamless.

apt-mark hold kubeadm kubectl kubelet 

# Indicate that the script has completed.

endsetup kubernetes
