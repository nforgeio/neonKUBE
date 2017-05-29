# -----------------------------------------------------------------------------
# NTP setup.
#
# NOTE: This script must be run under sudo.
#
# NOTE: macros of the form $(...) will be replaced by the deployer.

if [ ! -f $SETUP_DIR/configured/ntp ]; then

	# The Azure WAAGENT doesn't appear to do anything fancy with time and my understanding
	# is that the underlying Azure host machines don't update their clocks very often
	# (perhaps as infrequently as once a week).  This script installs a local NTP service
	# which will query external sources much more often (1 to 17 minutes) keeping the clock
	# in sync.

	apt-get -y install ntp
	service ntp stop
	cp ntp.conf /etc/ntp.conf
	service ntp start

	# Indicate that we've successfully configured NTP.

    echo CONFIGURED > $SETUP_DIR/configured/ntp
fi
