# This script builds and deploys the xt_DPORT iptables modules as required.

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

# Change to the directory hosting this script.

pushd "$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

# Build and install userspace module.

cp Makefile-so Makefile
make libxt_DPORT.so
cp libxt_DPORT.so /lib/xtables
chmod 644 /lib/xtables/libxt_DPORT.so
rm Makefile

# Build and install the kernel module.

cp Makefile-ko Makefile
make
cp xt_DPORT.ko /lib/modules/`uname -r`/kernel/net/netfilter
rm Makefile

# Load the kernel module if it's not already loaded.

if ! grep xt_DPORT /proc/modules &> /dev/nul ; then
    insmod /lib/modules/`uname -r`/kernel/net/netfilter/xt_DPORT.ko
fi

# Restore the directory.

popd
