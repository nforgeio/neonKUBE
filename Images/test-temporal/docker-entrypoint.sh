#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# container init
. ./container-init.sh

# Launch the service.

exec test-temporal
