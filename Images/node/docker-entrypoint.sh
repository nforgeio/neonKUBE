#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching the Node
# application.

# Run the hive host node environment script if present.

if [ -f /etc/neon/env-host ] ; then
    . /etc/neon/env-host
fi

set -e
node /program.js
