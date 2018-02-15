#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         go-build
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# usage: go-build
#
# This script simply builds the GO project named by $PROJECT.  The PROJECT
# name is passed as an argument when the container is started and the 
# $PROJECT environment variable is set by [docker-entrypoint.sh].
#
# Note that this file is named [go-build.sh] in the neonFORGE source repo
# but is renamed to just [go-build] when the container image is built.

# GO doesn't look for vendor files in the project directory so we'll 
# copy any project vendor files over to [/usr/local/go/src] before doing 
# the build.

if [ -d /src/$PROJECT/vendor ]; then
    cp -r /src/$PROJECT/vendor/* /usr/local/go
fi

# Do the build.

cd /src/$PROJECT
go build
