#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  NEONFORGE Team
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Launch the service.

exec neon-node-agent
