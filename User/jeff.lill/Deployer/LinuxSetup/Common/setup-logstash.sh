# -----------------------------------------------------------------------------
# Logstash setup.
#
# This script requires that the ambient $LOGSTASH_VERSION variable be set to
# the required Logstash software version.
#
# NOTE: This script must be run under sudo and that macros of the form $(...)
#       will be replaced by the deployer.

if [ ! -f $SETUP_DIR/configured/logstash ]; then

    echo
    echo "**********************************************"
    echo "** Installing Logstash                      **"
    echo "**********************************************"

	# Visit the following page for a description of the following
	# installation steps:
	#
	# https://www.elastic.co/guide/en/logstash/current/package-repositories.html

	wget -qO - https://packages.elasticsearch.org/GPG-KEY-elasticsearch | apt-key add -
	echo "deb http://packages.elasticsearch.org/logstash/1.5/debian stable main" | tee -a /etc/apt/sources.list
	apt-get -q update
	apt-get -q -y install logstash=$LOGSTASH_VERSION

	# It appears that the Logstash package is missing a dependency that's preventing
	# it from monitoring files.  This appears to be a reconized bug as described here:
	#
	#	https://github.com/elastic/logstash/issues/3127
	#
	# I'm going to go ahead and install this manually.  Hopefully we'll be able
	# to remove this with future Logstash releases.
	
	apt-get -q -y install libc6-dev

	# Create a NOOP config file that simply reads events from a never changing
	# file and then discards them so Logstash won't barf before useful config
	# file(s) are added to the config folder.

	mkdir -p /etc/logstash/conf.d
	echo "" >> /etc/logstash/conf.d/no-events
	echo 'input { file { path => "/etc/logstash/conf.d/no-events" } }' > /etc/logstash/conf.d/noop.conf

	# Configure the Upstart service configuration.

	cp logstash.init.conf /etc/init/logstash.conf

    # Indicate that we've successfully configured Logstash.

    echo CONFIGURED > $SETUP_DIR/configured/logstash

fi
