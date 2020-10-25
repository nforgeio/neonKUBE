#!/bin/bash -x

# Original code: Copyright (c) 2017 Uber Technologies, Inc.
# Modifications: Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

CADENCE_HOME=$1
UI_HOME=$2
SERVICES="history,matching,frontend,worker"

start_cassandra() {
    ./cassandra -R
}

wait_for_cassandra() {
    server=`echo $CASSANDRA_SEEDS | awk -F ',' '{print $1}'`
    until cqlsh --cqlversion=3.4.4 $server < /dev/null; do
        echo 'waiting for cassandra to start up'
        sleep 1
    done
    echo 'cassandra started'
}

init_env() {

    # cassandra env
    export CASSANDRA_SEEDS="127.0.0.1"
    export LOG_LEVEL="${LOG_LEVEL:-info}"
    export NUM_HISTORY_SHARDS=${NUM_HISTORY_SHARDS:-4}
    export KEYSPACE="${KEYSPACE:-cadence}"
    export VISIBILITY_KEYSPACE="${VISIBILITY_KEYSPACE:-cadence_visibility}"
    export CASSANDRA_CONSISTENCY="${CASSANDRA_CONSISTENCY:-One}"

    # cadence and UI
    export HOST_IP=`hostname --ip-address`
    export CADENCE_TCHANNEL_PEERS="$HOST_IP:7933"

    if [ "$BIND_ON_LOCALHOST" == true ] || [ "$BIND_ON_IP" == "127.0.0.1" ]; then
        export BIND_ON_IP="127.0.0.1"
        export HOST_IP="127.0.0.1"
    elif [ -z "$BIND_ON_IP" ]; then
        # not binding to localhost and bind_on_ip is empty - use default host ip addr
        export BIND_ON_IP=$HOST_IP
    elif [ "$BIND_ON_IP" != "0.0.0.0" ]; then
        # binding to a user specified addr, make sure HOST_IP also uses the same addr
        export HOST_IP=$BIND_ON_IP
    fi

    if [ -z "$RINGPOP_SEEDS" ]; then
        export RINGPOP_SEEDS_JSON_ARRAY="[\"$HOST_IP:7933\",\"$HOST_IP:7934\",\"$HOST_IP:7935\",\"$HOST_IP:7939\"]"
    else
        array=(${RINGPOP_SEEDS//,/ })
        export RINGPOP_SEEDS_JSON_ARRAY=$(json_array "${array[@]}")
    fi

    if [ -z "$STATSD_ENDPOINT" ]; then 
        export STATSD_ENDPOINT="$HOST_IP:8125"
    fi
}

json_array() {
  echo -n '['
  while [ $# -gt 0 ]; do
    x=${1//\\/\\\\}
    echo -n \"${x//\"/\\\"}\"
    [ $# -gt 1 ] && echo -n ', '
    shift
  done
  echo ']'
}

# start cassandra,
# wait for it to complete startup
init_env
start_cassandra
wait_for_cassandra

# inject environment variables into the .yaml config files
envsubst < config/docker_template_cassandra.yaml > config/docker.yaml

# start the frontend
node $UI_HOME/server.js & ./cadence-server --root $CADENCE_HOME --env docker start --services=$SERVICES
