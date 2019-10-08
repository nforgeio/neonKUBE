#!/bin/sh
#------------------------------------------------------------------------------
# Standard container initialization script.
#
# USAGE: ./container-init.sh

. ./log-info.sh "Container started."

START=`date +%s`
ENVOY_END=$((START+300))

if [ ! -e /dev/shm/envoy_shared_memory_0 ]; then
  . ./log-info.sh "Running without Istio sidecar, not waiting for Envoy."
  return 0
fi

if [ ! -z "${DEV_WORKSTATION+x}" ]; then
  . ./log-info.sh "Running on DEV_WORKSTATION, not waiting for Envoy."
  return 0
fi

. ./log-info.sh "Running in Kubernetes with Istio enabled."

until wget -q --spider 127.0.0.1:15000
do
  if [ $((`date +%s`)) -gt $ENVOY_END ]; then
    . ./log-error.sh "Envoy Sidecar not available, exiting."
    exit 1
  fi
  . ./log-info.sh "Waiting for Envoy Sidecar..."
  sleep 3
done

. ./log-info.sh "Envoy Sidecar available."

if [ ! -z "${ENVOY_LOGLEVEL+x}" ]; then
  . ./log-info.sh "Setting Envoy log level to debug."
  wget --post-data="" -O - "127.0.0.1:15000/logging?level=${ENVOY_LOGLEVEL}"
fi

return 0
