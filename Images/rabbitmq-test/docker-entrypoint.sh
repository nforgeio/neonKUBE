#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Launches RabbitMQ for unit testing purposes.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the neonHIVE constants.

. /neonhive.sh

# This is the path to a temporary RabbitMQ config file.  We're going to completely
# manage the contents of the configuration file by writing our settings here and
# then relying on a modified RabbitMQ docker entrypoint script (created by the 
# image Dockerfile) to copy this to the standard configuration file just before
# launching the RabbitMQ server.
#
# This is necessary because the RabbitMQ docker entrypoint script overwrites some
# of our settings.

config_path=/etc/rabbitmq/rabbitmq.neon.conf
touch $config_path

# Parse the environment variables.

if [ "$CLUSTER_PARTITION_MODE" == "" ] ; then
    export CLUSTER_PARTITION_MODE=autoheal
fi

if [ "$ERL_EPMD_PORT" == "" ] ; then
    export ERL_EPMD_PORT=4369
fi

if [ "$RABBITMQ_DEFAULT_USER" == "" ] ; then
    export RABBITMQ_DEFAULT_USER=Administrator
fi

if [ "$RABBITMQ_DEFAULT_PASS" == "" ] ; then
    export RABBITMQ_DEFAULT_PASS=password
fi

if [ "$RABBITMQ_NODE_PORT" == "" ] ; then
    export RABBITMQ_NODE_PORT=5672
fi

if [ "$RABBITMQ_DIST_PORT" == "" ] ; then
    export RABBITMQ_DIST_PORT=25672
fi

if [ "$RABBITMQ_MANAGEMENT_PORT" == "" ] ; then
    export RABBITMQ_MANAGEMENT_PORT=15672
fi

if [ "$RABBITMQ_ERLANG_COOKIE" == "" ] ; then

    if [ "$CLUSTER_NODES" != "" ] ; then
        . log-error.sh "[RABBITMQ_ERLANG_COOKIE] is required if [CLUSTER_NODES] is specified."
        exit 1
    fi
fi

MANAGEMENT_PLUGIN=true

if [ "$RABBITMQ_VM_MEMORY_HIGH_WATERMARK" == "" ] ; then
    export RABBITMQ_VM_MEMORY_HIGH_WATERMARK=0.5
fi

if [ "$RABBITMQ_DISK_FREE_LIMIT" == "" ] ; then
    RABBITMQ_DISK_FREE_LIMIT=1GB
fi

. log-info.sh "TLS is disabled."

unset RABBITMQ_SSL_CERTFILE
unset RABBITMQ_SSL_KEYFILE
unset RABBITMQ_SSL_CACERTFILE

# The hostname needs to be a FQDN.

export HOSTNAME=rmq-test.hive
echo 127.0.0.1 rmq-test.hive >> /etc/hosts

# Generate the RabbitMQ config file.

echo                                                                                        >> $config_path
echo "# Connection Settings"                                                                >> $config_path
echo                                                                                        >> $config_path
echo "listeners.tcp.default                       = 0.0.0.0:$RABBITMQ_NODE_PORT"            >> $config_path
echo                                                                                        >> $config_path
echo "management.listener.ip                      = 0.0.0.0"                                >> $config_path
echo "management.listener.port                    = $RABBITMQ_MANAGEMENT_PORT"              >> $config_path
echo "management.listener.ssl                     = false"                                  >> $config_path
echo                                                                                        >> $config_path
echo "heartbeat                                   = 60"                                     >> $config_path

# Other configuration settings:

hipe_compile=false
if [ "$RABBITMQ_HIPE_COMPILE" == "1" ] ; then
    hipe_compile=true
fi

echo                                                                                        >> $config_path
echo "# Other Settings"                                                                     >> $config_path
echo                                                                                        >> $config_path
echo "loopback_users                              = none"                                   >> $config_path
echo "default_user                                = $RABBITMQ_DEFAULT_USER"                 >> $config_path
echo "default_pass                                = $RABBITMQ_DEFAULT_PASS"                 >> $config_path
echo "hipe_compile                                = $hipe_compile"                          >> $config_path
echo "cluster_partition_handling                  = $CLUSTER_PARTITION_MODE"                >> $config_path

# We need to tell RabbitMQ about the actual RAM available to the container 
# due to any CGROUP limits so the server will be able to compute the relative
# RAM high watermark.

memTotalKb=
if [ -r /proc/meminfo ]; then
    memTotalKb="$(awk -F ':? +' '$1 == "MemTotal" { print $2; exit }' /proc/meminfo)"
fi
memLimitB=
if [ -r /sys/fs/cgroup/memory/memory.limit_in_bytes ]; then
    # "18446744073709551615" is a valid value for "memory.limit_in_bytes", which is too big for Bash math to handle
    # "$(( 18446744073709551615 / 1024 ))" = 0; "$(( 18446744073709551615 * 40 / 100 ))" = 0
    memLimitB="$(awk -v totKb="$memTotalKb" '{
        limB = $0;
        limKb = limB / 1024;
        if (!totKb || limKb < totKb) {
            printf "%.0f\n", limB;
        }
    }' /sys/fs/cgroup/memory/memory.limit_in_bytes)"
fi
if [ -n "$memLimitB" ]; then
    echo "total_memory_available_override_value       = $memLimitB"                         >> $config_path
fi

# RABBITMQ_VM_MEMORY_HIGH_WATERMARK is relative number to available RAM between [0.0 ... 1.0] 
# (with a decimal point) or an absolute number of bytes.

if [[ "$RABBITMQ_VM_MEMORY_HIGH_WATERMARK" =~ ^[0-9]+\.[0-9]+$ ]] ; then
    echo "vm_memory_high_watermark.relative           = $RABBITMQ_VM_MEMORY_HIGH_WATERMARK" >> $config_path
else
    echo "vm_memory_high_watermark.absolute           = $RABBITMQ_VM_MEMORY_HIGH_WATERMARK" >> $config_path
fi

# RABBITMQ_DISK_FREE_LIMIT is relative to available RAM between [0.0 ... 1.0] (with a decimal point)
# or an absolute number of bytes.

if [[ $RABBITMQ_DISK_FREE_LIMIT =~ ^[0-9]+\.[0-9]+$ ]] ; then
    echo "disk_free_limit.relative                    = $RABBITMQ_DISK_FREE_LIMIT"          >> $config_path
else
    echo "disk_free_limit.absolute                    = $RABBITMQ_DISK_FREE_LIMIT"          >> $config_path
fi

# Generate the Erlang cookie if the environment variable doesn't specify one.

if [ "$RABBITMQ_ERLANG_COOKIE" == "" ] ; then
    RABBITMQ_ERLANG_COOKIE=$(pwgen -s 20 1)
fi

# We need to persist the cookie to a couple of files
# so the admin tools will work.

echo $RABBITMQ_ERLANG_COOKIE > /var/lib/rabbitmq/.erlang.cookie
echo $RABBITMQ_ERLANG_COOKIE > $HOME/.erlang.cookie

chmod 600 /var/lib/rabbitmq/.erlang.cookie
chmod 600 $HOME/.erlang.cookie

if [ "$RABBITMQ_HIPE_COMPILE" != "1" ] ; then

    # RabbitMQ treats any non-empty string as enabling HiPE so we're going
    # to clear this for anything other than "1".
    
    export RABBITMQ_HIPE_COMPILE=''
fi

# We need this so RabbitMQ will use fully qualified hostnames.

export RABBITMQ_USE_LONGNAME=true

# Log the configuration variables and files to make debugging easier.

if [ "$DEBUG" == "true" ] ; then

    echo "============================ Environment Variables =================================="
    env | sort
    echo "================================= Config File ======================================="
    cat $config_path
    echo "====================================================================================="
fi

# Enable the management plugin.

if [ "$$MANAGEMENT_PLUGIN" == "true" ] ; then
    . log-info.sh "Enabling management plugin..."
    rabbitmq-plugins enable rabbitmq_management
    . log-info.sh "Management plugin enabled."
fi

# Start RabbitMQ via its modified Docker entrypoint script.

rabbitmq-entrypoint.sh rabbitmq-server
