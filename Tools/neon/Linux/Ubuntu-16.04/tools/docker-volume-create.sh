#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-volume-create.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Creates a local named Docker volume if it doesn't already exist.

if ! docker-volume-exists ${1} ; then
    docker volume create ${1}
fi
