#------------------------------------------------------------------------------
# FILE:         setup-ntp.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

# NOTE: This script must be run under sudo.
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# The Linux by default doesn't update the system clocks very often (perhaps as 
# infrequently as once on start in cloud services like Azure).  This installs
# a local NTP service which will query external sources much more often 
# (1 to 17 minutes) to help maintain accurate time.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-NTP                                **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup ntp

# Install NTP and configure.

safe-apt-get install -yq --allow-downgrades ntp
mkdir -p /var/log/ntp

cat <<EOF > /etc/ntp.conf
# For more information about this file, see the man pages
# ntp.conf(5), ntp_acc(5), ntp_auth(5), ntp_clock(5), ntp_misc(5), ntp_mon(5).

# Specifies that NTP will implicitly trust the time source, 
# even if it is way off from the current system time.  This
# can happen when:
#
#       1. The BIOS time is set incorrectly
#       2. The machine is a VM that woke up from a long sleep
#       3. The time source is messed up
#       4. Somebody is screwing with us by sending bad time responses
#
# Normally, NTP is configured to panic when it sees a difference
# of 1000 seconds or more between the source and the system time
# and NTP will log an error and terminate.
#
# NTP is configured to start with the [-g] option, which will 
# ignore 1000+ second differences when the service starts.  This
# handles issues #1 and #2 above.
#
# The setting below disables the 1000 second check and also allows
# NTP to modify the system clock in steps of 1 second (normally,
# this is much smaller).

# \$todo(jefflill):
#
# This was the default setting when NTP was installed but I'm not 
# entirely sure that this is what we'd want for production.  The
# [man ntp.conf] page specifically recommends not messing with
# these defaults.

tinker panic 0 dispersion 1.000

# Specifies the file used to track the frequency offset of The
# local clock oscillator.

driftfile /var/lib/ntp/ntp.drift

# Configure a log file.

logfile /var/log/ntp/ntp.log
 
# Permit time synchronization with our time source, but do not
# permit the source to query or modify the service on this system.

restrict default kod nomodify notrap nopeer noquery
restrict -6 default kod nomodify notrap nopeer noquery

# Permit all access over the loopback interface.  This could
# be tightened as well, but to do so would effect some of
# the administrative functions.

restrict 127.0.0.1
restrict -6 ::1

# Hosts on local networks are less restricted.

restrict 10.0.0.0 mask 255.0.0.0 nomodify notrap
restrict 169.254.0.0 mask 255.255.0.0 nomodify notrap
restrict 172.16.0.0 mask 255.255.0.0 nomodify notrap
restrict 192.168.0.0 mask 255.255.0.0 nomodify notrap

# Use local system time when the time sources are not reachable.

server  127.127.1.0 # local clock
fudge   127.127.1.0 stratum 10

# Specify the time sources.

EOF

# Append the time sources to the configuration file.
#
# We're going to prefer the first source.  For worker nodes
# this will be the first master node.  The nice thing about this
# is that all of the worker nodes will be using the same time
# source on the local network, so the clocks should be very
# close to being synchronized.

if [ "${NEON_NODE_ROLE}" == "master" ] ; then
    sources=${NEON_NTP_MASTER_SOURCES}
else
    sources=${NEON_NTP_WORKER_SOURCES}
fi

echo "*** TIME SOURCES = ${sources}" 1>&2

preferred_set=false

for source in ${sources}
do
    if ! ${preferred_set} ; then
        echo "server $source burst iburst prefer" >> /etc/ntp.conf
        preferred_set=true
    else
        echo "server $source burst iburst" >> /etc/ntp.conf
    fi
done

# Generate the [${NEON_BIN_FOLDER}/update-time] script.

cat <<EOF > ${NEON_BIN_FOLDER}/update-time
#!/bin/bash
#------------------------------------------------------------------------------
# This script stops the NTP time service and forces an immediate update.  This 
# is called from the NTP init.d script (as modified below) to ensure that we 
# obtain the current time on boot (even if the system clock is way out of sync).
# This may also be invoked manually.
#
# Usage:
#
#       update-time [--norestart]
#
#       --norestart - Don't restart NTP

. $<load-cluster-conf-quiet>

restart=true

for arg in \$@
do
    if [ "\${arg}" = "--norestart" ] ; then
        restart=false
    fi
done

if \${restart} ; then
    service ntp stop
fi

ntpdate ${sources[@]}

if \${restart} ; then
    service ntp start
fi
EOF

chmod 700 ${NEON_BIN_FOLDER}/update-time

# Edit the NTP [/etc/init.d/ntp] script to initialize the hardware clock and
# call [update-time] before starting NTP.

cat <<"EOF" > /etc/init.d/ntp
#!/bin/sh

### BEGIN INIT INFO
# Provides:        ntp
# Required-Start:  $network $remote_fs $syslog
# Required-Stop:   $network $remote_fs $syslog
# Default-Start:   2 3 4 5
# Default-Stop:    1
# Short-Description: Start NTP daemon
### END INIT INFO

PATH=/sbin:/bin:/usr/sbin:/usr/bin

. /lib/lsb/init-functions

DAEMON=/usr/sbin/ntpd
PIDFILE=/var/run/ntpd.pid

test -x $DAEMON || exit 5

if [ -r /etc/default/ntp ]; then
    . /etc/default/ntp
fi

if [ -e /var/lib/ntp/ntp.conf.dhcp ]; then
    NTPD_OPTS="$NTPD_OPTS -c /var/lib/ntp/ntp.conf.dhcp"
fi

LOCKFILE=/var/lock/ntpdate

lock_ntpdate() {
    if [ -x /usr/bin/lockfile-create ]; then
        lockfile-create $LOCKFILE
        lockfile-touch $LOCKFILE &
        LOCKTOUCHPID="$!"
    fi
}

unlock_ntpdate() {
    if [ -x /usr/bin/lockfile-create ] ; then
        kill $LOCKTOUCHPID
        lockfile-remove $LOCKFILE
    fi
}

RUNASUSER=ntp
UGID=$(getent passwd $RUNASUSER | cut -f 3,4 -d:) || true
if test "$(uname -s)" = "Linux"; then
        NTPD_OPTS="$NTPD_OPTS -u $UGID"
fi

case $1 in
    start)
        log_daemon_msg "Starting NTP server" "ntpd"
        if [ -z "$UGID" ]; then
            log_failure_msg "user \"$RUNASUSER\" does not exist"
            exit 1
        fi

        #------------------------------
        # This is the modification.

		# This bit of voodoo disables Hyper-V time synchronization with The
		# host server.  We don't want this because the cluster is doing its
		# own time management and time sync will fight us.  This is described
		# here:
		#
		# https://social.msdn.microsoft.com/Forums/en-US/8c0a1026-0b02-405a-848e-628e68229eaf/i-have-a-lot-of-time-has-been-changed-in-the-journal-of-my-linux-boxes?forum=WAVirtualMachinesforWindows

		log_daemon_msg "Start: Disabling Hyper-V time synchronization" "ntpd"
		echo 2dd1ce17-079e-403c-b352-a1921ee207ee > /sys/bus/vmbus/drivers/hv_util/unbind
		log_daemon_msg "Finished: Disabling Hyper-V time synchronization" "ntpd"
        
        log_daemon_msg "Start: Updating current time" "ntpd"
        ${NEON_BIN_FOLDER}/update-time --norestart
        log_daemon_msg "Finished: Updating current time" "ntpd"

        #------------------------------

        lock_ntpdate
        start-stop-daemon --start --quiet --oknodo --pidfile $PIDFILE --startas $DAEMON -- -p $PIDFILE $NTPD_OPTS
        status=$?
        unlock_ntpdate
        log_end_msg $status
        ;;
    stop)
        log_daemon_msg "Stopping NTP server" "ntpd"
        start-stop-daemon --stop --quiet --oknodo --pidfile $PIDFILE
        log_end_msg $?
        rm -f $PIDFILE
        ;;
    restart|force-reload)
        $0 stop && sleep 2 && $0 start
        ;;
    try-restart)
        if $0 status >/dev/null; then
            $0 restart
        else
            exit 0
        fi
        ;;
    reload)
        exit 3
        ;;
    status)
        status_of_proc $DAEMON "NTP server"
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|try-restart|force-reload|status}"
        exit 2
        ;;
esac
EOF

# Restart NTP to ensure that we've picked up the current time.

service ntp restart

# Indicate that the script has completed.

endsetup ntp
