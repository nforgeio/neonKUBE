#!/bin/bash

# This script is invoked just before the main image entrypoint
# script starts Couchbase.  This script will attempt to initialize 
# the cluster, retrying until the Couchbase is ready.

# Set default cluster parameters if these environment variables
# were not passed into the container.

if [ "${CLUSTER_NAME}" == "" ] ; then
    CLUSTER_NAME=test
fi

if [ "${USERNAME}" == "" ] ; then
    USERNAME=Administrator
fi

if [ "${PASSWORD}" == "" ] ; then
    PASSWORD=password
fi

if [ "${CLUSTER_RAM_MB}" == "" ] ; then
    CLUSTER_RAM_MB=256
fi

if [ "${FTS_RAM_MB}" == "" ] ; then
    FTS_RAM_MB=256
fi

if [ "${INDEX_RAM_MB}" == "" ] ; then
    INDEX_RAM_MB=256
fi

if [ "${BUCKET_NAME}" == "" ] ; then
    BUCKET_NAME=test
fi

if [ "${BUCKET_RAM_MB}" == "" ] ; then
    BUCKET_RAM_MB=256
fi

echo "*** Waiting for Couchbase to start..."

while : 
do
    # Give Couchbase a chance to start and then attempt
    # to initialize the cluster.

    sleep 1
    couchbase-cli cluster-init --cluster-name ${CLUSTER_NAME} \
        --cluster-username ${USERNAME} \
        --cluster-password ${PASSWORD} \
        --cluster-ramsize ${CLUSTER_RAM_MB} \
        --cluster-fts-ramsize ${FTS_RAM_MB} \
        --cluster-index-ramsize ${INDEX_RAM_MB} \
        --services data,index,query,fts

    if [ "$?" == "0" ] ; then
        break
    fi
done

echo "*** Couchbase is running."

# The cluster is ready, so create the bucket.  Note that we're
# enabling FLUSH so it will be easy to clear the bucket state
# for unit tests.

echo "*** Creating bucket: ${BUCKET_NAME}"

couchbase-cli bucket-create \
    -u ${USERNAME} \
    -p ${PASSWORD} \
    --cluster localhost:8091 \
    --bucket ${BUCKET_NAME} \
    --bucket-type couchbase \
    --bucket-ramsize ${BUCKET_RAM_MB} \
    --enable-flush 1

# ...and then create the user account with full cluster admin rights.

echo "*** Creating bucket: ${BUCKET_NAME}"

couchbase-cli user-manage \
    -u ${USERNAME} \
    -p ${PASSWORD} \
    --set \
    --cluster localhost:8091 \
    --rbac-username ${USERNAME} \
    --rbac-password ${PASSWORD} \
    --auth-domain local \
    --roles admin

 echo "*** Couchbase is READY!"
