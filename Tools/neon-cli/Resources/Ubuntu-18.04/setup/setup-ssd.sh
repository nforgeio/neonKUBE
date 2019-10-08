#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-ssd.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

# $todo(jefflill):
#
# I need to research whether I need to do additional Linux tuning of the RAID
# chunk size and other SSD related file system parameters.  The article below
# describes some of this.  It's from 2009 though so it may be out of date.
# Hopefully modern Linux distributions automatically tune for SSDs.
#
#		http://blog.nuclex-games.com/2009/12/aligning-an-ssd-on-linux/

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-SSD                                **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup ssd

if ! ${NEON_NODE_HDD} ; then

    echo "*** BEGIN: Tuning for SSD" 1>&2

    # This script works by generating the [${NEON_BIN_FOLDER}/neon-tune-ssd] script so that it
    # configures up to 2 boot and 8 data [sd?] devices to:
    #
    #       * Use the [deadline] scheduler
    #       * Indicate that the device does not rotate
    #       * Sets the read-ahead value
    #
    # Then the script configures a systemd service unit file that calls the script
    # during system boot.  Here's the official Debian SSD optimization suggestions:
    #
    #   https://wiki.debian.org/SSDOptimization

    read_ahead_size_kb=64

    # Generate [${NEON_BIN_FOLDER}/neon-tune-ssd]

    rm -f ${NEON_BIN_FOLDER}/neon-tune-ssd

    cat <<EOF > ${NEON_BIN_FOLDER}/neon-tune-ssd
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-tune-ssd
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

# This script is generated during setup by [setup-ssd.sh] to execute
# the commands necessary to properly tune any attached SSDs

EOF

	# Note that $<node.driveprefix> will be replaced with something
	# like [sd] or [xvd].  This comes from the hosting manager used to
	# provision the cluster.

    for DEVICE in $<node.driveprefix>a $<node.driveprefix>b $<node.driveprefix>c $<node.driveprefix>d $<node.driveprefix>e $<node.driveprefix>f $<node.driveprefix>g $<node.driveprefix>h $<node.driveprefix>i $<node.driveprefix>j
    do
        if [ -d /sys/block/$DEVICE ]; then
            echo " "                                                                   >> ${NEON_BIN_FOLDER}/neon-tune-ssd
            echo "# DEVICE: $DEVICE"                                                   >> ${NEON_BIN_FOLDER}/neon-tune-ssd
            echo "# ---------------"                                                   >> ${NEON_BIN_FOLDER}/neon-tune-ssd
            echo "echo deadline > /sys/block/$DEVICE/queue/scheduler"                  >> ${NEON_BIN_FOLDER}/neon-tune-ssd
            echo "echo 0 > /sys/block/$DEVICE/queue/rotational"                        >> ${NEON_BIN_FOLDER}/neon-tune-ssd
            echo "echo ${read_ahead_size_kb} > /sys/block/$DEVICE/queue/read_ahead_kb" >> ${NEON_BIN_FOLDER}/neon-tune-ssd
        fi
    done

    chmod 700 ${NEON_BIN_FOLDER}/neon-tune-ssd

    # Configure and start the [neon-tune-ssd] systemd service.

    cat <<EOF > /lib/systemd/system/neon-tune-ssd.service
[Unit]
Description=SSD Tuning Service
Documentation=
After=sysinit.target
Requires=

[Service]
Type=oneshot
ExecStart=${NEON_BIN_FOLDER}/neon-tune-ssd

[Install]
WantedBy=multi-user.target
EOF

    systemctl enable neon-tune-ssd
	systemctl daemon-reload
    systemctl start neon-tune-ssd

    echo "*** END: Tuning for SSD" 1>&2
else
    echo "*** SSD tuning is disabled" 1>&2
fi

# Indicate that the script has completed.

endsetup ssd
