#!/bin/bash
#
# This script invokes the [neon-cli] command that implements 
# this Ansible module.

# This acts as a marker telling Ansible that we want JSON formatted arguments.

WANT_JSON=yes

# This command must run be run on the Ansible master within 
# the [neon-cli] container.

if [ "${IN_NEON_ANSIBLE_COMMAND}" == "" ] ; then

    echo "*** ERROR: The [neon_couchbase_import] module runs only on the Ansible master.  Consider using: [delegate_to: localhost]" 1>&2
    exit 1
fi

# Invoke the module as a [neon-cli] command.

neon ansible module neon_couchbase_import $@
