#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-containx.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Installs the ContainX Docker Docker NFS volume plugin.
#
#		https://github.com/ContainX/docker-volume-netshare

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/
#
# NOTE: I'm not using [-u] here to avoid failing for undefined
#       environment variables because some won't be initialized
#       when prepping nodes without a full cluster definition.

set -eo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-CONTAINX-VOLUME PLUGIN             **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-config>
. setup-utility.sh

# Install the [apt-cacher-ng] service on manager nodes.

if [ ${NEON_HOST_CONTAINX_ENABLED} ] ; then

	# Download and install the ContainX volume plugin.

	curl -4fsSLv ${CURL_RETRY} ${NEON_HOST_CONTAINX_PACKAGEURL} -o /tmp/containx.deb 1>&2
	dpkg -i /tmp/containx.deb
	rm /tmp/containx.deb

	# Configure the plugin to be started by systemd.

	cat <<EOF > /lib/systemd/system/docker-volume-netshare-nfs.service
[Unit]
Description=Containx docker-volume-netshare plugin service for NFS backed volumes
Documentation=
After=
Requires=
Before=

[Service]
Type=simple
ExecStart=/usr/bin/docker-volume-netshare nfs

[Install]
WantedBy=docker.service
EOF

	# Restrict this to ROOT because it may be modified in the future 
	# to include remote share credentials.

	chmod 700 /lib/systemd/system/docker-volume-netshare-nfs.service

	# Load, enable, and start the service.

	systemctl daemon-reload
	systemctl enable docker-volume-netshare-nfs
	systemctl restart docker-volume-netshare-nfs
fi
