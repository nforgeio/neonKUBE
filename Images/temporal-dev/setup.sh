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

TEMPORAL_HOME=$1
DB=${DB:-cassandra}
RF=${RF:-1}

# cassandra env

export KEYSPACE="${KEYSPACE:-temporal}"
export VISIBILITY_KEYSPACE="${VISIBILITY_KEYSPACE:-temporal_visibility}"
export CASSANDRA_SEEDS="127.0.0.1"

setup_schema() {
    echo 'setup cassandra schema'

    # cassandra schema

    SCHEMA_DIR=$TEMPORAL_HOME/schema/cassandra/temporal/versioned
    temporal-cassandra-tool --ep $CASSANDRA_SEEDS create -k $KEYSPACE --rf $RF
    temporal-cassandra-tool --ep $CASSANDRA_SEEDS -k $KEYSPACE setup-schema -v 0.0
    temporal-cassandra-tool --ep $CASSANDRA_SEEDS -k $KEYSPACE update-schema -d $SCHEMA_DIR
    
    # visibility schema

    VISIBILITY_SCHEMA_DIR=$TEMPORAL_HOME/schema/cassandra/visibility/versioned
    temporal-cassandra-tool --ep $CASSANDRA_SEEDS create -k $VISIBILITY_KEYSPACE --rf $RF
    temporal-cassandra-tool --ep $CASSANDRA_SEEDS -k $VISIBILITY_KEYSPACE setup-schema -v 0.0
    temporal-cassandra-tool --ep $CASSANDRA_SEEDS -k $VISIBILITY_KEYSPACE update-schema -d $VISIBILITY_SCHEMA_DIR
}

start_cassandra() {
    envsubst < /etc/cassandra/cassandra_template.yaml > /etc/cassandra/cassandra.yaml
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

# start cassandra,
# wait for it to complete startup,
# set up the schema

start_cassandra
wait_for_cassandra
setup_schema

# sleep for 1 second to make sure that all writes have been completed

sleep 1
