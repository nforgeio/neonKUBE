#------------------------------------------------------------------------------
# Upgrades all packages for initial installation.
#
# NOTE: This script must be run under sudo.

if [ ! -f $SETUP_DIR/configured/upgrade-linux ]; then

    echo
    echo "**********************************************"
    echo "** Upgrading Linux (all packages)           **"
    echo "**********************************************"

	apt-get -q update
	apt-get -q -y upgrade

    # Indicate that we've successfully upgraded Linux.

    echo CONFIGURED > $SETUP_DIR/configured/upgrade-linux

fi
