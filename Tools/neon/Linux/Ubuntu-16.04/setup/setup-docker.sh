#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-docker.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [node-conf]
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
echo "** SETUP-DOCKER                             **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-config>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup setup-docker

#--------------------------------------------------------------------------
# Note we're going to delete the docker unique key file if present.  Docker will
# generate a unique key the first time it starts.  It's possible that a key
# might be left over if the OS image was cloned.  Docker Swarm won't schedule
# containers on nodes with duplicate keys.

rm -f /etc/docker/key.json

#--------------------------------------------------------------------------
# We want the Docker containers, volumes, and other files to be located on
# the attached data drive (if there is one) rather than the OS drive.  Node
# preparation should have configured [/mnt-data] to be a link to the data
# drive or simply be a physical directory on the OS drive so we'll link
# the root Docker storage directory [/var/lib/docker] to [/mnt-data/docker]

mkdir -p /mnt-data/docker
ln -s /mnt-data/docker /var/lib/docker

#--------------------------------------------------------------------------
# Install Docker
#
#   https://docs.docker.com/engine/installation/linux/ubuntulinux/
#
# Note that NEON_DOCKER_VERSION can be set to [latest] (the default),
# [test], [experimental], [development] or a version number like [17.03.0-ce].
#
# [latest], [test], and [experimental] install the current published
# releases as described at https://github.com/docker/docker/releases
# using the standard Docker setup scripts.
#
# Specifying a straight version number like [17.03.0-ce] installs a specific
# package, as described here:
#
#   https://docs.docker.com/engine/installation/linux/docker-ce/ubuntu/

# IMPORTANT!
#
# Production clusters should install Docker with a specific version number
# to ensure that you'll be able to deploy additional hosts with the
# same Docker release as the rest of the cluster.  This also prevents 
# the package manager from inadvertently upgrading Docker.

docker_version=

case "${NEON_DOCKER_VERSION}" in

test)

    curl -4fsSLv ${CURL_RETRY} https://test.docker.com/ | sh 1>&2
    touch ${NEON_STATE_FOLDER}/docker
    ;;

experimental)

    curl -4fsSLv ${CURL_RETRY} https://experimental.docker.com/ | sh 1>&2
    touch ${NEON_STATE_FOLDER}/docker
    ;;

latest)

    curl -4fsSLv ${CURL_RETRY} https://get.docker.com/ | sh 1>&2
    touch ${NEON_STATE_FOLDER}/docker
    ;;

*)
    # Specific Docker version requested.  We'll set ${binary_uri}
    # to the URI for binary and perform the actual installation below.

    docker_version=${NEON_DOCKER_VERSION}
    ;;

esac

if [ "${docker_version}" != "" ] ; then

	# Install prerequisites.

	apt-get install -yq apt-transport-https ca-certificates curl software-properties-common

	# Configure the stable, edge, and testing repositorties

	add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
	add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) edge"
	add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) testing"

	# Install a specific Docker version.
	#
	# This command lists the available versions:
	#
	#		apt-cache madison docker-ce

	# $todo(jeff.lill): SECURITY RISK
	#
	#	--allow-unauthenticated below is a security risk.

	apt-get update
	apt-get install -yq --allow-unauthenticated docker-ce=${docker_version}
fi

#--------------------------------------------------------------------------
# We need to overwrite the Docker systemd unit file with one that has our
# custom Docker options.  We'll save the Docker service file installed by
# the package manager to [~/.save/docker/docker.service].  We may need to
# restore this when upgrading  Docker in the future.
#
# Note that this file won't be present if we installed from binaries.

if [ -f /lib/systemd/system/docker.service ] ; then

    mkdir -p ${NEON_ARCHIVE_FOLDER}/docker
    cp /lib/systemd/system/docker.service ${NEON_ARCHIVE_FOLDER}/docker/docker.service
fi

cat <<EOF > /lib/systemd/system/docker.service
# Modified from the original installed by Docker to add custom command
# line options generated by [neon-cli] as well as explicit restart options.

[Unit]
Description=Docker Application Container Engine
Documentation=https://docs.docker.com
After=network.target
After=
Requires=
Before=

[Service]
Type=notify
# The default is not to use systemd for cgroups because the delegate issues still
# exists and systemd currently does not support the cgroup feature set required
# for containers run by docker
ExecStart=/usr/bin/dockerd --graph /mnt-data/docker $<docker.options>
ExecReload=/bin/kill -s HUP \$MAINPID
# Rate limit Docker restarts
Restart=always
RestartSec=2s
# Having non-zero Limit*s causes performance problems due to accounting overhead
# in the kernel. We recommend using cgroups to do container-local accounting.
LimitNOFILE=infinity
LimitNPROC=infinity
LimitCORE=infinity
# Uncomment TasksMax if your systemd version supports it.
# Only systemd 226 and above support this version.
TasksMax=infinity
TimeoutStartSec=0
# Set delegate yes so that systemd does not reset the cgroups of docker containers
Delegate=yes
# Kill only the docker process, not all processes in the cgroup
KillMode=process

[Install]
WantedBy=multi-user.target
EOF

# Configure Docker to start on boot and then restart it to pick up the new options.

systemctl enable docker
systemctl restart docker

# We relocated the Docker graph root directory to [/mnt-data/docker] so we can
# remove any of the old files at the default location.

if [ -d /var/lib/docker ] ; then
	rm -r /var/lib/docker
fi

# Indicate that the script has completed.

endsetup setup-docker
