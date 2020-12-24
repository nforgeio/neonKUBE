#!/bin/sh
#------------------------------------------------------------------------------
# Maintains a global count of the log events emitted by a container and Sets
# the LOG_INDEX variable to the index of the next event to be emitted.
#
# The global state is maintained in the [/dev/shm/log-index] file, maintained
# in the shared in-memory file system.  Note that there is no file locking so
# it's possible with multiple processes logging events in parallel for events
# to be tagged with the same order value, but this shouldn't be a problem in
# real life.
#
# USAGE: . /log-index.sh

LOG_INDEX=$(cat /dev/shm/log-index 2> /dev/null)

if [ ! $? ] ; then
    # [log-index] file doesn't exist so initialize it.
    LOG_INDEX=0
fi

LOG_INDEX=$(( LOG_INDEX + 1 ))

echo $LOG_INDEX > /dev/shm/log-index
