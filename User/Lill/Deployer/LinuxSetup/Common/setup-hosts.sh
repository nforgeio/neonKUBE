# -----------------------------------------------------------------------------
# Initialize the HOSTS file.
#
# NOTE: This script must be run under sudo.
#
# NOTE: Macros like $(...) will be replaced by the Deployer.

if [ ! -f $SETUP_DIR/configured/hosts ]; then

    echo
    echo "**********************************************"
    echo "** Initializing HOSTNAME and HOSTS files    **"
    echo "**********************************************"

	# Initialize the hostname.

	echo $(serverName) | tee /etc/hostname

    # We need to add a loopback address mapping for the VMs unqualified and
    # fully qualified host names.

    echo | tee -a /etc/hosts
    echo "# VM host mappings" | tee -a /etc/hosts
    echo 127.0.0.1 $(serverName) $(serverName).$(serviceDomain) | tee -a /etc/hosts

    # Indicate that we've successfully completed the host configuration.

    echo CONFIGURED > $SETUP_DIR/configured/hosts
fi

