#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
#
# usage: docker-entrypoint PROJECT COMMAND...
#
# Changes the current directory to [/src/PROJECT] before executing COMMAND.
# 
# Build Outputs
# -------------
# The [go build] command generates an executable named PROJECT within the 
# PROJECT directory.  This is inconvenient when using source control because
# we don't want to check in build outputs.  This script works around this.
# 
# If COMMAND completes with [exitcode=0] and a [src/PROJECT/PROJECT] file
# exists afterwards, then this file will be moved to the [bin] subdirectory
# (creating the directory if necessary).  This works well for environments
# where [bin] folders have been added to [.gitignore].

function usage {

    echo
    echo "usage: docker run --rm -v PROJECT-PATH:/src nhive/golang PROJECT go build" 
    echo
    echo "where: PROJECT-PATH   - path to the GO project folder on your workstation"
    echo "       PROJECT        - project name"
    echo
}

# Add any scripts in the root folder to the PATH.

export PATH=$PATH:/

# Verify that we have at least two arguments.

if [ $# -lt 2 ] ; then
    usage
    exit 1
fi

export PROJECT=${1}

# Verify that the source folder was mapped into the container.

if [ ! -d /src ]; then
    usage
    exit 1
fi

# Verify that the project subfolder actually exists.

if [ ! -d /src/${PROJECT} ]; then
    echo "*** ERROR: The [${PROJECT}] sub-folder doesn't exist.  Be sure to"
    echo "           map the parent folder into the container and then specify"
    echo "           the PROJECT folder as the first parameter."
    echo
    usage
    exit 1
fi

# Change the directory and then execute the command.

cd /src/${PROJECT}
${*:2}
exitcode=$?

# Check for build executable output and move it to a
# [bin] subdirectory.

if [ ${exitcode} -ne 0 ]; then
    exit ${exitcode}
fi

if [ -f /src/${PROJECT}/${PROJECT} ]; then

    if [ ! -d /src/${PROJECT}/bin ]; then
        mkdir /src/${PROJECT}/bin
    fi

    cp /src/${PROJECT}/${PROJECT} /src/${PROJECT}/bin
    rm /src/${PROJECT}/${PROJECT}
fi
