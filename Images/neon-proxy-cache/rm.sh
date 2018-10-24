#!/bin/bash

# $hack(jeff.lill): This is a horrible HACK!
#
# Unfortunately, [varnishd] attempts to remove the [/var/lib/varnish/_.vsm_mgt]
# directory when it starts but this will fail because it needs to be mounted
# as a TMPFS.  We don't actually need the directory to be removed because 
# we know that its already empty when the container starts.
#
# The hack is to replace the standard [rm] command with our own script that
# ignores this command (and calls the original [rm] for all other arguments):
#
#       rm -rf _.vsm_mgt
#
# We know this is the command Varnish will issue because we instrumented
# a temporary [rm] script to log all calls to a file.

# This is the temporary operation logging code:

# echo $@ >> /rm.log
# /bin/rm-org "$@"

# Here's the workaround code:

if [ "$1" != "-rf" ] || [ "$2" != "_.vsm_mgt" ] ; then
    bin/rm-org "$@"
fi
