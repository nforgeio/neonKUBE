# -----------------------------------------------------------------------------
# Misc Linux setup.
#
# NOTE: This script must be run under sudo.
#
# NOTE: macros of the form $(...) will be replaced by the deployer.

if [ ! -f $SETUP_DIR/configured/linux ]; then

    echo
    echo "**********************************************"
    echo "** Installing Linux Packages                **"
    echo "**********************************************"

	apt-get -q update

    # Install [sysstat]

    apt-get -q -y install sysstat

    # Install [dstat]

    apt-get -q -y install dstat

    # Install [iotop]

    apt-get -q -y install iotop

    # Install [iptraf]

    apt-get -q -y install iptraf

	# Install Apache tools (e.g. the [htpasswd] utility)

	apt-get -q -y install apache2-utils

	# Install the Nano editor

	apt-get -q -y install nano

	# Install [unzip]

	apt-get -q -y install unzip

    echo
    echo "**********************************************"
    echo "** More Linux Setup                         **"
    echo "**********************************************"

    # Enable system statistics collection (e.g. Page Faults,...)

    sed -i '/^ENABLED="false"/c\ENABLED="true"' /etc/default/sysstat

    # Configure WAAGENT to format and mount the resource file system
    # and create a SWAP file there on the next reboot.

    sed -i '/^ResourceDisk.Format=n/c\ResourceDisk.Format=y' /etc/waagent.conf
    sed -i '/^ResourceDisk.EnableSwap=n/c\ResourceDisk.EnableSwap=y' /etc/waagent.conf

	# Cassandra doesn't want to swap so we're going to comment out the
	# creation of a swap file below.  We may want to make this configurable
	# by the Deployer.

    # sed -i '/^ResourceDisk.SwapSizeMB=0/c\ResourceDisk.SwapSizeMB=$(swapSizeMB)' /etc/waagent.conf

    # Indicate that we've successfully configured Linux.

    echo CONFIGURED > $SETUP_DIR/configured/linux

fi
