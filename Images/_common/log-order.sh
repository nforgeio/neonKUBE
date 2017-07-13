#!/bin/sh
#------------------------------------------------------------------------------
# Maintains a global count of the log events emitted by a container and Sets
# the LOG_ORDER variable to the index of the next event to be emitted.
#
# The global state is maintained in the [/dev/shm/log-order] file, maintained
# in the shared in-memory file system.  Note that there is no file locking so
# it's possible with multiple processes logging events in parallel for events
# to be tagged with the same order value, but this shouldn't be a problem in
# real life.
#
# USAGE: . /log-order

LOG_ORDER=$(cat /dev/shm/log-order) &> /dev/null

if [ ! $? ] ; then
    # [log-order] file doesn't exist so initialize it.
    LOG_ORDER=0
fi

let LOG_ORDER=$LOG_ORDER+1

echo $LOG_ORDER > /dev/shm/log-order
