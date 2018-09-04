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

# This is the path to the RabbitMQ config file.

config_path=/etc/rabbitmq/rabbitmq.conf

# Parse the environment variables.

if [ "$NODENAME" != "" ] ; then

    # Split the fully qualified RabbitMQ node name into its simple name
    # and fully qualified hostname parts.

    node_name=$(echo $NODENAME | cut -d '@' -f 1)
    node_host=$(echo $NODENAME | cut -d '@' -f 2)
    
    # We need to update [/etc/hostname] and the HOSTNAME environment variable.

    echo $node_host > /etc/hostname
    export HOSTNAME=$node_host

    # And we also need to set RABBITMQ_NODE_HOSTNAME

    export RABBITMQ_NODENAME=$NODENAME
fi

if [ "$CLUSTER_PARTITION_MODE" == "" ] ; then
    export CLUSTER_PARTITION_MODE=autoheal
fi

if [ "$CLUSTER_NODES" != "" ] ; then

    # This environment variable lists necessary information about the RabbitMQ
    # cluster nodes.  Each cluster node will have an entry like NODENAME@HOSTNAME,
    # where NODENAME is a simple name like "manager-0" and hostname is the resolvable
    # hostname or IP address of the node like "manager-0.neon-rabbitmq.MYHIVE.nhive.io".
    # The node definitions are to be separated by commas.
    
    # Clear any existing configuration file.

    echo > $config_path
    chmod 644 $config_path

    echo "# Static config-based cluster discovery."                                        >> $config_path
    echo                                                                                   >> $config_path
    echo "cluster_formation.peer_discovery_backend = rabbit_peer_discovery_classic_config" >> $config_path

    cluster_nodes=$(echo $CLUSTER_NODES | tr "," "\n")

    index=1
    for node in $cluster_nodes
    do
        node_name=$(echo $node | cut -d '@' -f 1)
        node_host=$(echo $node | cut -d '@' -f 2)

        echo "cluster_formation.classic_config.nodes.$index = $node_name@$node_host" >> $config_path
        index=$((index + 1))
    done
fi
    
if [ "$ERL_EPMD_PORT" == "" ] ; then
    export ERL_EPMD_PORT=4369
fi

if [ "$RABBITMQ_DEFAULT_USER" == "" ] ; then
    export RABBITMQ_DEFAULT_USER=sysadmin
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
    cp "$RABBITMQ_SSL_CACERTFILE" /etc/rabbitmq/cert/hiveca.crt
    chmod 666 /etc/rabbitmq/cert/*

    export RABBITMQ_SSL_CERTFILE=/etc/rabbitmq/cert/hive.crt
    export RABBITMQ_SSL_KEYFILE=/etc/rabbitmq/cert/hive.key
    export RABBITMQ_SSL_CACERTFILE=/etc/rabbitmq/cert/hiveca.crt

    echo                                                                           >> $config_path
    echo "# TLS Configuration"                                                     >> $config_path
    echo                                                                           >> $config_path
    echo "listeners.tcp.default                   = 65123"                         >> $config_path
    echo "listeners.ssl.default                   = $RABBITMQ_NODE_PORT"           >> $config_path
    echo "ssl_options.certfile                    = /etc/rabbitmq/cert/hive.crt"   >> $config_path
    echo "ssl_options.keyfile                     = /etc/rabbitmq/cert/hive.key"   >> $config_path
    echo "ssl_options.cacertfile                  = /etc/rabbitmq/cert/hiveca.crt" >> $config_path
    echo "ssl_options.verify                      = verify_none"                   >> $config_path
    echo "ssl_options.fail_if_no_peer_cert        = false"                         >> $config_path
    echo                                                                           >> $config_path
    echo "management.listener.ssl                 = true"                          >> $config_path
    echo "management.listener.ssl_opts.certfile   = /etc/rabbitmq/cert/hive.crt"   >> $config_path
    echo "management.listener.ssl_opts.keyfile    = /etc/rabbitmq/cert/hive.key"   >> $config_path
    echo "management.listener.ssl_opts.cacertfile = /etc/rabbitmq/cert/hiveca.crt" >> $config_path

elif [[ "$TLS_CERT_FILE" == "" && "$TLS_KEY_FILE" == "" ]] ; then
    . log-info.sh "TLS is disabled."
else
    . log-error.sh "One of [RABBITMQ_SSL_CERTFILE] or [RABBITMQ_SSL_KEYFILE-FILE] is missing."
    exit 1
fi

# Other configuration settings:

echo                                                           >> $config_path
echo "# Other Settings"                                        >> $config_path
echo                                                           >> $config_path
echo "cluster_partition_handling  = $CLUSTER_PARTITION_MODE"   >> $config_path
echo "disk_free_limit.absolute    = $RABBITMQ_DISK_FREE_LIMIT" >> $config_path
echo "loopback_users              = none"                      >> $config_path

# $todo(jeff.lill):
#
# I'm not sure if these statistics related settings are a good
# idea.  This appeared to cause some trouble a few years back.

echo "collect_statistics          = fine"  >> $config_path
echo "collect_statistics_interval = 30000" >> $config_path

echo                                            >> $config_path
echo "# RabbitMQ Entrypoint Generated Settings" >> $config_path
echo                                            >> $config_path

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

# Set the cluster name if specified.

if [ "$CLUSTER_NAME" != "" ] ; then
    rabbitmqctl set_cluster_name $CLUSTER_NAME
fi

# Enable the management components.

rabbitmq-plugins enable rabbitmq_management

# It appears that RabbitMQ can have issues forming a cluster when all of the
# nodes are started at the same time.  The most recent post to this issue:
#
#   https://groups.google.com/forum/#!topic/rabbitmq-users/DpQGo_UTjG8
#
# from 08-2018 indicates that this is still a problem for v3.7.7 and that 
# the current mitigation is to introduce random delays before starting each
# instance.  It appears that they're talking about adding retries in the 
# future.
#
# We're going to go ahead and sleep for a random number of seconds between
# 0 and 30 to mitigate this when clustering is enabled and this is the
# first time to container is started.

restarted_path=/var/lib/rabbitmq/.restarted

if [ ! -f $restarted_path ] ; then

    if [ "$CLUSTER_NODES" != "" ] ; then
        delay=$(shuf -i 5-30 -n 1)
        . log-info.sh "Delaying start by [$delay] seconds to mitigate a cluster formation race."
        sleep $delay
    fi

    touch $restarted_path
fi

# sleep 100000000
# . /rabbitmq-entrypoint.sh rabbitmq-server
bash /rabbitmq-entrypoint.sh rabbitmq-server
sleep 100000000000
