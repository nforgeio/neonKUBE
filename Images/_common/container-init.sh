#!/bin/bash -e
#------------------------------------------------------------------------------
# Standard container initialization script.
#
# USAGE: . ./container-init.sh


envoy_end=$((SECONDS+300))

if [[ -z "${DEV_WORKSTATION}" ]]; then
  . ./log-info.sh "Running in Kubernetes with Istio enabled."
  until curl --head --silent --output /dev/null localhost:15000
  do
    if [ $SECONDS -gt $envoy_end ]; then
      . ./log-error.sh "Envoy Sidecar not available, exiting."
      exit 1
    fi
    . ./log-info.sh "Waiting for Envoy Sidecar..."
    sleep 3
  done
else
  . ./log-info.sh "Running on DEV_WORKSTATION, not waiting for Envoy."
  exit 0
fi

. ./log-info.sh "Envoy Sidecar available."

exit 0
