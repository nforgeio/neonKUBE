# -----------------------------------------------------------------------------
# Install Elasticsearch.
#
# NOTE: This script must be run under sudo.

if [ ! -f $SETUP_DIR/configured/elasticsearch ]; then

    echo
    echo "**********************************************"
    echo "** Installing Elasticsearch                 **"
    echo "**********************************************"

	# Install the Elasticsearch package:
	#
	# The following article describes what we're doing here:
	#
	# https://www.elastic.co/guide/en/elasticsearch/reference/current/setup-repositories.html

	wget -qO - https://packages.elastic.co/GPG-KEY-elasticsearch | apt-key add -
	echo "deb http://packages.elastic.co/elasticsearch/1.7/debian stable main" | tee -a /etc/apt/sources.list.d/elasticsearch-1.7.list
	apt-get -q update
	apt-get -q -y install elasticsearch=$ELASTIC_VERSION

	# Make sure the service is stopped.

	service elasticsearch stop

	# Configure the temporary folder.

	mkdir -p /mnt/temp
	chown elasticsearch:elasticsearch /mnt/temp

	# Configure the data folder.

    chown elasticsearch:elasticsearch /mnt-data

	# Configure the log folder.

	mkdir -p /var/log/elasticsearch

	# Copy the configuration files.

	cp elasticsearch.yml /etc/elasticsearch
	cp logging.yml /etc/elasticsearch

	# Configure the Elasticsearch to start at boot.

	cp elasticsearch.init.conf /etc/init/elasticsearch.conf

    # Indicate that we've successfully installed Elasticsearch.

    echo CONFIGURED > $SETUP_DIR/configured/elasticsearch

fi
