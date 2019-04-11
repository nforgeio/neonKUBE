#!/bin/bash

# Original Code: Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# Bash script when run makes the go-cadence-proxy golang project.  It will build
# both a windows and linux amd64 Go executables.  The executables can be found in
# $NF_ROOT/Build.
# Logs for the build can be found in NF_ROOT\Build\go-logs

#  Set PROJECTPATH
# set NF_ROOT
# take blackslashes out
# set GOPATH
# set BUILDLOGSPATH
# set PROXYPATH
# cd into PROXYPATH
PROJECTPATH="/src/github.com/loopieio/go-cadence-proxy"
NF_ROOT_BSLASH=${NF_ROOT}
NF_ROOT_FSLASH="${NF_ROOT_BSLASH//\\//}"
GOPATH="${NF_ROOT_FSLASH}/Go"
BUILDLOGSPATH="${NF_ROOT_FSLASH}/Build/go-logs"
PROXYPATH="${GOPATH}${PROJECTPATH}" && cd ${PROXYPATH}


# execute the makefile and log the output into a logs folder
{
    rm -f "${BUILDLOGSPATH}/build-proxy.log"
    make -f Makefile >> "${BUILDLOGSPATH}/build-proxy.log" && echo "***GO-CADENCE-PROXY BUILD SUCCESSFUL!!!***: Go executables in ${NF_ROOT}\Build"
} || {
    exit
    echo "***ERROR: GO-CADENCE-PROXY BUILD FAILED***: Check build logs in ${NF_ROOT}\Build\go-logs"
}

# cd back to NF_ROOT
cd ${NF_ROOT}