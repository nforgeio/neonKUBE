#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-volume-create
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Creates a local named Docker volume if it doesn't already exist.  Pass the
# volume name as the first paramater and then pass up to 8 additional --level
# options.

if ! docker-volume-exists ${1} ; then
    docker volume create ${2} ${3} ${4} ${5} ${6} ${7} ${8} ${9} ${1}
fi
