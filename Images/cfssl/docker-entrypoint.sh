#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# usage: docker-entrypoint COMMAND...
#
# This image provides the CloudFlare [cfssl] for managing SSL certificates
# and certificate authorities.  This entrypoint changes the current directory
# to [/ca] before executing COMMAND so the command can access the files there.
#
#       https://github.com/cloudflare/cfssl 

function usage {

    echo
    echo "usage: docker run --rm -v PROJECT-PATH:/ca nhive/cfssl COMMAND..." 
    echo
    echo "where: COMMAND    - the  command and arguments"
    echo
}

# Add any scripts in the root folder to the PATH.

export PATH=$PATH:/

# Verify that we have at least one argument.

if [ $# -lt 1 ] ; then
    usage
    exit 1
fi

# Verify that the ca was mapped into the container.

if [ ! -d /ca ]; then
    mkdir -p /ca
fi

# Change the directory and then execute the command.

cd /ca
${*:1}
exitcode=$?
