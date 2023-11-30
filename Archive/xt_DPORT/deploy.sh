#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         deploy.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script builds and deploys the xt_DPORT iptables modules as required.

# Make sure that we have loaded the standard environment variables.

. /etc/neon/hive.conf.sh

# $hack(jeff.lill):
#
# I barely understand how Linux/iptables modules are built and
# installed, but this hack seems to work.

# NOTE:
#
# We're always going to rebuild the modules even if they already exist
# to ensure that the modules get rebuilt after the kernel is upgraded.
# Note that deployment may fail if the modules are already loaded 
# (e.g. if the [neon-iptables] service is restarted) but this should
# be OK the modules would have already been rebuilt for the current
# kernel.
#
# The only real assumption here is that the host node is rebooted after
# upgrading the Linux kernel.  [neon hive upgrade linux] does this.

# We need to rebuild the modules after every reboot in case the 
# Linux kernel was upgraded.  We're going to use the presence of
# the [xt-DROP-built] file in a tmpfs to indicate that the module
# has been built already.  Since this is on a RAM drive, the file
# will no longer exist after a reboot, indicating that the modules
# need to be rebuilt.

mkdir -p ${NEON_TMPFS_FOLDER}

if [ ! -f ${NEON_TMPFS_FOLDER}/xt-DROP-built ] ; then

    echo "[INFO] Building iptables DPORT target extension."

    # Change to the directory hosting this script.

    pushd "$( cd "$( dirname "${BASH_SOURCE[0]}" )" &>/dev/null && pwd )"

    # Build and install the userspace module.

    cp Makefile-so Makefile
    
    if [ -f libxt_DPORT.so ] ; then
        rm libxt_DPORT.so
    fi
    
    make libxt_DPORT.so
    cp libxt_DPORT.so /lib/xtables/libxt_DPORT.so
    chmod 644 /lib/xtables/libxt_DPORT.so
    rm Makefile

    # Build and install the kernel module.

    cp Makefile-ko Makefile
    
    if [ -f xt_DPORT.ko ] ; then
        rm xt_DPORT.ko
    fi

    make
    cp xt_DPORT.ko /lib/modules/`uname -r`/kernel/net/netfilter/xt_DPORT.ko
    chmod 644 /lib/modules/`uname -r`/kernel/net/netfilter/xt_DPORT.ko
    rm Makefile

    # Indicate that the modules were built or at least we tried
    # to build them.  We're not going to try again.

    touch ${NEON_TMPFS_FOLDER}/xt-DROP-built

    # Restore the directory.

    popd
fi

# Load the kernel module if it's not already loaded.

if ! grep xt_DPORT /proc/modules &> /dev/nul ; then

    echo "[INFO] Loading iptables DPORT target extension."

    # Ensure that iptables is loaded first by listing some rules
    # and then load our module.

    iptables -S PREROUTING -t raw &> /dev/nul
    insmod /lib/modules/`uname -r`/kernel/net/netfilter/xt_DPORT.ko
fi
