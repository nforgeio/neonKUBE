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
    
    config_path=/etc/rabbitmq/rabbitmq.conf

    # Clear any existing configuration file.

    echo > $config_path
    chmod 644 $config_path

    echo "# We're doing static config-based cluster discovery."                            >> $config_path
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
    
    # Other cluster related configuration.

    echo "cluster_partition_handling = $CLUSTER_PARTITION_MODE" >> $config_path
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

# We need this so RabbitMQ will allow fully qualified host names.

export RABBITMQ_USE_LONGNAME=true

# Set the cluster name if specified.

if [ "$CLUSTER_NAME" != "" ] ; then
    rabbitmqctl set_cluster_name $CLUSTER_NAME
fi

# Enable the management UI and then start RabbitMQ.

rabbitmq-plugins enable rabbitmq_management
. /rabbitmq-entrypoint.sh rabbitmq-server
