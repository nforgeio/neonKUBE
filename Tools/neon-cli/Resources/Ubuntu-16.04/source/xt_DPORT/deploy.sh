# This script builds and deploys the xt_DPORT iptables modules as required.

# $hack(jeff.lill):
#
# I barely understand how Linux/iptables modules are built and
# installed, but this hack seems to work.

# Build and install userspace module.

if [ ! -f /lib/xtables/libxt_DPORT.so ] ; then

    cp Makefile-so Makefile
    make libxt_DPORT.so
    cp libxt_DPORT.so /lib/xtables
    chmod 644 /lib/xtables/libxt_DPORT.so
    rm Makefile
fi

# Build and install the kernel module.

if [ ! -f /lib/modules/`uname -r`/kernel/net/netfilter/xt_DPORT.ko ] ; then

    cp Makefile-ko Makefile
    make
    cp xt_DPORT.ko /lib/modules/`uname -r`/kernel/net/netfilter
    rm Makefile
fi

# Load the kernel module if it's not already loaded.

if ! grep xt_DPORT /proc/modules &> /dev/nul ; then
    insmod /lib/modules/`uname -r`/kernel/net/netfilter/xt_DPORT.ko
fi
