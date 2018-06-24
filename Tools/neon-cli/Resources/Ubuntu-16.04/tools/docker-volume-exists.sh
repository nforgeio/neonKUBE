#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-volume-exists
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Determines whether a named local volume exists.  The exit code will be 0
# if the volume exists, 1 otherwise.

# Here's what we're doing:
#
#   1. List the volumes
#   2. Add line numbers
#   3. Strip out the first line and print the volume name
#   4. Match named volume

docker volume list \
    | awk '{printf("%d %s\n", NR, $0)}' \
    | awk '$1>1 {print $3}' \
    | grep -q ${1}
