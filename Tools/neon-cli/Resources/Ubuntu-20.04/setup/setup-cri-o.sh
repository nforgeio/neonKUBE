#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-cri-o.sh
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script handles the installation of the Docker CLI.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-CRI-O                              **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup cri-o

# Create the .conf file to load the modules at bootup
cat <<EOF | sudo tee /etc/modules-load.d/crio.conf
overlay
br_netfilter
EOF

sudo modprobe overlay
sudo modprobe br_netfilter

sysctl --system

OS=xUbuntu_20.04
VERSION=1.18

cat <<EOF | sudo tee /etc/apt/sources.list.d/devel:kubic:libcontainers:stable.list
deb https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/${OS}/ /
EOF
cat <<EOF | sudo tee /etc/apt/sources.list.d/devel:kubic:libcontainers:stable:cri-o:${VERSION}.list
deb http://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/${VERSION}/${OS}/ /
EOF

curl -L https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/${OS}/Release.key | apt-key --keyring /etc/apt/trusted.gpg.d/libcontainers.gpg add -
curl -L https://download.opensuse.org/repositories/devel:kubic:libcontainers:stable:cri-o:${VERSION}/${OS}/Release.key | apt-key --keyring /etc/apt/trusted.gpg.d/libcontainers-cri-o.gpg add -

apt-get update -y
apt-get install -y cri-o cri-o-runc

cat <<EOF | sudo tee /etc/containers/registries.conf
unqualified-search-registries = [ "$<neon-branch-registry>", "docker.io", "quay.io", "registry.access.redhat.com", "registry.fedoraproject.org"]

[[registry]]
prefix = "$<neon-branch-registry>"
insecure = false
blocked = false
location = "$<neon-branch-registry>"
[[registry.mirror]]
location = "registry.neon-system"

[[registry]]
prefix = "docker.io"
insecure = false
blocked = false
location = "docker.io"
[[registry.mirror]]
location = "registry.neon-system"

[[registry]]
prefix = "quay.io"
insecure = false
blocked = false
location = "quay.io"
[[registry.mirror]]
location = "registry.neon-system"
EOF

cat <<EOF | sudo tee /etc/crio/crio.conf.d/01-cgroup-manager.conf
[crio.runtime]
cgroup_manager = "systemd"
EOF

# We need to do a [daemon-reload] so systemd will be aware of the new unit drop-in.

systemctl disable crio
systemctl daemon-reload

# Configure CRI-O to start on boot and then restart it to pick up the new options.

systemctl enable crio
systemctl restart crio

# Prevent the package manager from automatically upgrading the Docker engine.

set +e      # Don't exit if the next command fails
apt-mark hold crio cri-o-runc

# Indicate that the script has completed.

endsetup cri-o
