#------------------------------------------------------------------------------
# Configures the machine to be an Elasticsearch cluster router.
#
# NOTE: This script must be run under sudo.

# $todo(jeff.lill): Implement a proper log rotation strategy for Nginx,
#                   Kibana, and Elasticsearch.

# These parameters need to be modified as necessary by the deployment tool 
# (or manually) to customize the configuration for the specific deployment.

ELASTIC_VERSION=1.7.2			# The Elasticsearch version to be installed
KIBANA_VERSION=4.1.2			# The Kibana version to be installed
NGINX_VERSION=1.8.0-1~trusty	# The Nginx version to be installed
LOGSTASH_VERSION=1:1.5.2-1		# The Logstash version to be installed
JAVA_VERSION=8					# Can be "7" or "8"
RAID_CHUNK_SIZE_KB=64			# The RAID chunk size in KB
READ_AHEAD_SIZE_KB=0			# The DATA drive read-ahead size in KB (must be a power of 2)
LOCAL_DISK=sda 			        # The local data drive (this will be [sdb] for Azure VMs)
LOCAL_SSD=true                  # The data drive is SSD backed

# -----------------------------------------------
# @DEPLOYER-OVERRIDES@
# -----------------------------------------------

# NOTE: This script assumes that it's been copied to the [~/setup] folder on the
#       target machine manually or by an automated process in TEXT mode and that
#       any carriage return characters have been stripped.

SETUP_DIR=~/setup

mkdir -p $SETUP_DIR/configured
cd $SETUP_DIR

# Disable any UX from commands like: apt-get

export DEBIAN_FRONTEND=noninteractive

#------------------------------------------------------------------------------
# Server Setup

. ./setup-ntp.sh
. ./setup-disk.sh
. ./setup-hosts.sh
. ./setup-java.sh
. ./setup-linux.sh
. ./setup-dotnet.sh
. ./setup-logstash.sh

# -----------------------------------------------------------------------------
# Install Nginx
#
# Visit these pages for more information:
#
#	Installation:	http://nginx.org/en/linux_packages.html
#   Documentation:	http://nginx.org/en/docs/
#
# Use this command to list the available package versions:
#
#	apt-cache showpkg nginx

echo
echo "**********************************************"
echo "** Installing Nginx                         **"
echo "**********************************************"

# NOTE: 
#
# I haven't been able to find or implement a clean Upstart configuration
# file for Nginx.  The seems to stem from Nginx forking additional processes
# on start.  So start seems to work fine, but Upstart isn't properly able to
# find the process to stop it.  Here's a link discussing this:
#
#	http://serverfault.com/questions/143461/how-can-i-start-nginx-via-upstart
#
# The net result is that Nginx will need to be managed with the older tools
# (not a big deal):
#
#		sudo service nginx start
#		sudo service nginx reart
#		sudo service nginx stop

if [ ! -f $SETUP_DIR/configured/nginx ]; then

	# Install the package.

	curl http://nginx.org/keys/nginx_signing.key > nginx-signing.key
	sudo apt-key add nginx-signing.key
	echo deb http://nginx.org/packages/ubuntu/ trusty nginx >> /etc/apt/sources.list
	apt-get -q update
	apt-get -q -y install nginx=$NGINX_VERSION

	# Generate the password file to be used for Nginx authentication.

	. ./setup-nginx-passwd.sh

	# Copy the config file and restart Nginx to pick up the changes.

	cp nginx.conf /etc/nginx/nginx.conf
	service nginx restart

	# Indicate that we've successfully installed Nginx.

    echo CONFIGURED > $SETUP_DIR/configured/nginx

fi

# -----------------------------------------------------------------------------
# Install Elasticsearch.

# This is a router VM and we didn't mount any SSDs.  So, we'll manually create
# a data folder just in case a Elasticsearch gateway needs one.

mkdir -p /mnt-data

. ./setup-elasticsearch.sh

# -----------------------------------------------------------------------------
# Install Kibana

echo
echo "**********************************************"
echo "** Installing Kibana                        **"
echo "**********************************************"

if [ ! -f $SETUP_DIR/configured/kibana ]; then

	# $todo(jeff.lill): Update the Kibana password in its config file.

	# Here's what we need to do:
	#
	#	1. Download the 64-bit version of the archive with the Kibana bits.
	#
	#	2. Extract the contents.  This will end up as a folder named for
	#      the Kibana version, like:
	#
	#			kibana-4.1.1-linux-x64
	#
	#	3. Delete any existing files in [/usr/share/kibana] just in case
	#      another version of Kitana is already installed.
	#
	#	4. Move the contents of the extracted folder to [/usr/share/kibana]
	#	   and then delete the now empty extraction folder.
	#
	#   5. Create the [kibana] system user and group.
	#
	#   6. Configure the Kibana log folder.
	#
	#   7. Copy the Kibana configuration file to the install location.
	#
	#   8. Copy the [kibana.init.conf] file to [/etc/init] so to enable
	#      Kibana to start at system boot, then start the service.

	KIBANA_URL="https://download.elastic.co/kibana/kibana/kibana-$KIBANA_VERSION-linux-x64.tar.gz"
	KIBANA_EXTRACT_FOLDER="kibana-$KIBANA_VERSION-linux-x64"
	KIBANA_BIN_FOLDER=/usr/share/kibana

	echo Downloading: $KIBANA_URL
	curl $KIBANA_URL > kibana.tar.gz
	tar xvf kibana.tar.gz

	if [ -d /usr/share/kibana ]; then
		rm -r /usr/share/kibana
	fi

	mkdir -p /usr/share/kibana
	mv $KIBANA_EXTRACT_FOLDER/* /usr/share/kibana
	rm -r $KIBANA_EXTRACT_FOLDER

	adduser --system --group kibana

	mkdir -p /var/log/kibana
	chown kibana:kibana /var/log/kibana

	cp kibana.yml /usr/share/kibana/config/kibana.yml

	cp kibana.init.conf /etc/init/kibana.conf
	start kibana

	# Indicate that we've successfully installed Kibana.

    echo CONFIGURED > $SETUP_DIR/configured/kibana

fi

echo
echo "**********************************************"
echo "**              REBOOT NOW!                 **"
echo "**********************************************"

exit 0
