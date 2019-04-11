#!/bin/bash

# Original Code: Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# Bash script when run makes the go-cadence-proxy golang project.  It will build
# both a windows and linux amd64 Go executables.  The executables can be found in
# $NF_ROOT/Build.
# Logs for the build can be found in NF_ROOT/Go/logs

#  Set PROJECTPATH, set GOPATH, take blackslashes out, set PROXYPATH and then cd into it
PROJECT_PATH="/src/github.com/loopieio/go-cadence-proxy"
GOPATH_BACKSLASH="${NF_ROOT}\Go"
GOPATH="${GOPATH_BACKSLASH//\\//}"
PROXYPATH="${GOPATH}${PROJECT_PATH}" && cd ${PROXYPATH}

# execute the makefile and log the output into a logs folder
{
    rm -f "${GOPATH}/logs/build-proxy.log"
    make -f Makefile >> "${GOPATH}/logs/build-proxy.log" && echo "***GO-CADENCE-PROXY BUILD SUCCESSFUL!!!***: Go executables in ${NF_ROOT}\Build"
} || {
    exit
    echo "***ERROR: GO-CADENCE-PROXY BUILD FAILED: Check build logs in ${GOPATH_BACKSLASH}\logs***"
}

# cd back to NF_ROOT
cd ${NF_ROOT}