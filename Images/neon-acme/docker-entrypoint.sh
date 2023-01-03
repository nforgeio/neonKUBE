#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Launch the service.

exec neon-acme
