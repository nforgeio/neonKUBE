#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching RabbitMQ.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the host node environment.

if [ -f /etc/neon/host-env ] ; then
    . /etc/neon/host-env
fi

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

if [ "$NODENAME" != "" ] ; then

    # Split the fully qualified RabbitMQ node name into its simple name
    # and fully qualified hostname parts.

    node_name=$(echo $NODENAME | cut -d '@' -f 1)
    node_host=$(echo $NODENAME | cut -d '@' -f 2)
    
    # We need to update [/etc/hostname] and the HOSTNAME environment variable.

    echo $node_host > /etc/hostname
    export HOSTNAME=$node_host

    # And we also need to set RABBITMQ_NODENAME

    export RABBITMQ_NODENAME=$NODENAME
fi

if [ "$CLUSTER_PARTITION_MODE" == "" ] ; then
    export CLUSTER_PARTITION_MODE=autoheal
fi

if [ "$CLUSTER_NODES" != "" ] ; then

    # This environment variable lists necessary information about the RabbitMQ
    # cluster nodes.  Each cluster node will have an entry like NODENAME@HOSTNAME,
    # where NODENAME is a simple name like "manager-0" and hostname is the resolvable
    # hostname or IP address of the node like "manager-0.neon-hivemq.MYHIVE.nhive.io".
    # The node definitions are to be separated by commas.
    
    # Clear any existing configuration file.

    touch $config_path
    chmod 644 $config_path

    echo "# Static config-based cluster discovery."                                             >> $config_path
    echo                                                                                        >> $config_path
    echo "cluster_formation.peer_discovery_backend    = rabbit_peer_discovery_classic_config"   >> $config_path

    cluster_nodes=$(echo $CLUSTER_NODES | tr "," "\n")

    index=1
    for node in $cluster_nodes
    do
        node_name=$(echo $node | cut -d '@' -f 1)
        node_host=$(echo $node | cut -d '@' -f 2)

        echo "cluster_formation.classic_config.nodes.$index    = $node_name@$node_host"         >> $config_path
        index=$((index + 1))
    done
fi
    
if [ "$ERL_EPMD_PORT" == "" ] ; then
    export ERL_EPMD_PORT=4369
fi

if [ "$RABBITMQ_DEFAULT_USER" == "" ] ; then
    export RABBITMQ_DEFAULT_USER=HiveConst_DefaultUsername
fi

if [ "$RABBITMQ_DEFAULT_PASS" == "" ] ; then
    export RABBITMQ_DEFAULT_PASS=HiveConst_DefaultPassword
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

if [ "$MANAGEMENT_PLUGIN" == "" ] ; then
    MANAGEMENT_PLUGIN=false
fi

if [ "$RABBITMQ_VM_MEMORY_HIGH_WATERMARK" == "" ] ; then
    export RABBITMQ_VM_MEMORY_HIGH_WATERMARK=0.5
fi

if [ "$RABBITMQ_DISK_FREE_LIMIT" == "" ] ; then
    RABBITMQ_DISK_FREE_LIMIT=500MB
fi

# TLS related configuration.

if [[ "$RABBITMQ_SSL_CERTFILE" != "" && "$RABBITMQ_SSL_KEYFILE" != "" ]] ; then

    . log-info.sh "TLS is enabled."

    if [ ! -f "$RABBITMQ_SSL_CERTFILE" ] ; then
        . log-error.sh "File [RABBITMQ_SSL_CERTFILE=$RABBITMQ_SSL_CERTFILE] does not exist."
        exit 1
    fi

    if [ ! -f "$RABBITMQ_SSL_KEYFILE" ] ; then
        . log-error.sh "File [RABBITMQ_SSL_KEYFILE=$RABBITMQ_SSL_KEYFILE] does not exist."
        exit 1
    fi

    if [ "$RABBITMQ_SSL_CACERTFILE" == "" ] ; then

        # RabbitMQ seems to always require a CA cert file, even if we're
        # disabling client checks so we'll simply use the main certificate
        # and hope for the best.

        export RABBITMQ_SSL_CACERTFILE=$RABBITMQ_SSL_CERTFILE
    fi

    # We need to copy the certificate and private key to another directory
    # so we can change their permissions to be readable by RabbitMQ.

    mkdir -p /etc/rabbitmq/cert
    chmod 777 /etc/rabbitmq/cert
    cp "$RABBITMQ_SSL_CERTFILE"   /etc/rabbitmq/cert/hive.crt
    cp "$RABBITMQ_SSL_KEYFILE"    /etc/rabbitmq/cert/hive.key
    cp "$RABBITMQ_SSL_CACERTFILE" /etc/rabbitmq/cert/hive.ca.crt
    chmod 666 /etc/rabbitmq/cert/*

    export RABBITMQ_SSL_CERTFILE=/etc/rabbitmq/cert/hive.crt
    export RABBITMQ_SSL_KEYFILE=/etc/rabbitmq/cert/hive.key
    export RABBITMQ_SSL_CACERTFILE=/etc/rabbitmq/cert/hive.ca.crt

    echo                                                                                    >> $config_path
    echo "# Connection Settings"                                                            >> $config_path
    echo                                                                                    >> $config_path
    echo "listeners.ssl.1                             = 0.0.0.0:$RABBITMQ_NODE_PORT"        >> $config_path
    echo "ssl_options.certfile                        = /etc/rabbitmq/cert/hive.crt"        >> $config_path
    echo "ssl_options.keyfile                         = /etc/rabbitmq/cert/hive.key"        >> $config_path
    echo "ssl_options.cacertfile                      = /etc/rabbitmq/cert/hive.ca.crt"     >> $config_path
    echo "ssl_options.verify                          = verify_none"                        >> $config_path
    echo "ssl_options.fail_if_no_peer_cert            = false"                              >> $config_path
    echo                                                                                    >> $config_path
    echo "management.listener.ip                      = 0.0.0.0"                            >> $config_path
    echo "management.listener.port                    = $RABBITMQ_MANAGEMENT_PORT"          >> $config_path
    echo "management.listener.ssl                     = true"                               >> $config_path
    echo "management.listener.ssl_opts.certfile       = /etc/rabbitmq/cert/hive.crt"        >> $config_path
    echo "management.listener.ssl_opts.keyfile        = /etc/rabbitmq/cert/hive.key"        >> $config_path
    # echo "management.listener.ssl_opts.cacertfile     = /etc/rabbitmq/cert/hive.ca.crt"     >> $config_path

    # Set the main TCP listener to use an unused and unpublished port to avoid
    # having RabbitMQ try to have its TCP and SSL listeners try to listen on 
    # the same port.

    export RABBITMQ_NODE_PORT=65123

elif [[ "$TLS_CERT_FILE" == "" && "$TLS_KEY_FILE" == "" ]] ; then

    . log-info.sh "TLS is disabled."

    unset RABBITMQ_SSL_CERTFILE
    unset RABBITMQ_SSL_KEYFILE
    unset RABBITMQ_SSL_CACERTFILE

    echo                                                                                    >> $config_path
    echo "# Connection Settings"                                                            >> $config_path
    echo                                                                                    >> $config_path
    echo "listeners.tcp.default                       = 0.0.0.0:$RABBITMQ_NODE_PORT"        >> $config_path
    echo                                                                                    >> $config_path
    echo "management.listener.ip                      = 0.0.0.0"                            >> $config_path
    echo "management.listener.port                    = $RABBITMQ_MANAGEMENT_PORT"          >> $config_path
    echo "management.listener.ssl                     = false"                              >> $config_path
else
    . log-error.sh "One of [RABBITMQ_SSL_CERTFILE] or [RABBITMQ_SSL_KEYFILE_FILE] is missing."
    exit 1
fi

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

# $todo(jeff.lill):
#
# I'm not sure if these statistics related settings are a good
# idea.  This appeared to cause some trouble a few years back.

echo "collect_statistics                          = fine"                                   >> $config_path
echo "collect_statistics_interval                 = 30000"                                  >> $config_path

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

# Before starting RabbitMQ, execute a script that will run in parallel, waiting for
# the server to report being ready before setting the cluster name (if there is one).

if [ "$CLUSTER_NAME" != "" ] ; then
    bash set-cluster-name.sh "$CLUSTER_NAME" &
fi

# Start RabbitMQ via its modified Docker entrypoint script.

rabbitmq-entrypoint.sh rabbitmq-server
