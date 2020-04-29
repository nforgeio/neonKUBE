#!/bin/bash -x

# Original code: Copyright (c) 2017 Uber Technologies, Inc.
# Modifications: Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.

# cassandra env

export CASSANDRA_SEEDS="127.0.0.1"
export LOG_LEVEL="${LOG_LEVEL:-info}"
export NUM_HISTORY_SHARDS=${NUM_HISTORY_SHARDS:-4}
export KEYSPACE="${KEYSPACE:-temporal}"
export VISIBILITY_KEYSPACE="${VISIBILITY_KEYSPACE:-temporal_visibility}"
export CASSANDRA_CONSISTENCY="${CASSANDRA_CONSISTENCY:-One}"

# tctl env

export TEMPORAL_CLI_ADDRESS="$BIND_ON_IP:7233"
export DEFAULT_NAMESPACE=${DEFAULT_NAMESPACE:-default}
export DEFAULT_NAMESPACE_RETENTION=${DEFAULT_NAMESPACE_RETENTION:-1}

# ui

export TEMPORAL_GRPC_ENDPOINT="$BIND_ON_IP:7233"
export TEMPORAL_WEB_PORT="8088"

start_cassandra() {
    cassandra -R
}

wait_for_cassandra() {
    server=`echo $CASSANDRA_SEEDS | awk -F ',' '{print $1}'`
    until cqlsh --cqlversion=3.4.4 $server < /dev/null; do
        echo 'waiting for cassandra to start up'
        sleep 1
    done
    echo 'cassandra started'
}

register_default_namespace() {
    echo "Temporal CLI Address: $TEMPORAL_CLI_ADDRESS"
    sleep 5
    echo "Registering default namespace: $DEFAULT_NAMESPACE"
    until tctl --ns $DEFAULT_NAMESPACE namespace describe < /dev/null; do
        echo "Default namespace $DEFAULT_NAMESPACE not found.  Creating..."
        sleep 1
        tctl --ns $DEFAULT_NAMESPACE namespace register --rd $DEFAULT_NAMESPACE_RETENTION --desc "Default namespace for Temporal Server"
    done
    echo "Default namespace registration complete."
}

# start cassandra,
# wait for it to complete startup
# register the default namespace in background

init_env
start_cassandra
wait_for_cassandra
register_default_namespace &

# start the temporal server and ui

bash /start-temporal.sh & bash /start-temporal-ui.sh
