#------------------------------------------------------------------------------
# Configures the machine to be a TokuMX database server.
#
# NOTE: This script must be run under sudo.

# These parameters need to be modified as necessary by the deployment tool 
# (or manually) to customize the configuration for the specific deployment.

TOKUMX_VERSION=2.0.1-1			# The TokuMX version to be installed
LOGSTASH_VERSION=1:1.5.2-1  	# The Logstash version to be installed
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
# Install and configure TokuMX.

if [ ! -f $SETUP_DIR/configured/tokumxd ]; then

    echo
    echo "**********************************************"
    echo "** Installing TokuMX (data)                 **"
    echo "**********************************************"

	# Install TokuMX.

	. ./setup-tokumx.sh

	# The TokuMX package installs the [tokumx.conf] Upstart script.  We're
	# going to stop the service if its running and delete the script to prevent
	# it from starting on boot.  This will be replaced by the [tokumxd.conf]
	# and [tokumxc.conf] Upstart scripts below that control the DATA and CONFIG
	# server instances.

	stop tokumx
	rm /etc/tokumx.conf
	rm /etc/init/tokumx.conf
	rm /var/log/tokumx/tokumx.log
	
    # We're going to initialize two instances of TokuMX.  The main database
    # will be listening on port 27017 and will host its data at [/mnt-data/tokumxd].
    # The second database is used to host shard configuration and will listen
    # on 27018 and will host its data at [/mnt-data/tokumxc].
	#
    # Create the instance folders if they don't already exist and grant
    # ownership to the [tokumx] user.

    mkdir -p /mnt-data/tokumxd
    mkdir -p /mnt-data/tokumxc

    chown tokumx:tokumx /mnt-data
    chown tokumx:tokumx /mnt-data/tokumxd
    chown tokumx:tokumx /mnt-data/tokumxc

    # ------------------------------------
    # Create the [/etc/tokumx] folder and secure it.

    mkdir -p /etc/tokumx
    chown tokumx:tokumx /etc/tokumx

    # ------------------------------------
    # Copy the cluster authentication key and secure it.

    cp cluster.key /etc/tokumx/cluster.key
    chown tokumx /etc/tokumx/cluster.key
    chmod 600 /etc/tokumx/cluster.key

    # ------------------------------------
    # Configure the DATA TokuMX instance.

    # The [tokumxd.conf] file will have been copied to the setup directory by
    # the Cluster Deployer with the specific settings for the VM and deployment.  
    # We'll copy this to the proper directory and restart the instance to 
    # pick  up the changes.

    cp tokumxd.conf /etc/tokumxd.conf

	# The [tokumxd.init.conf] file will replace the [tokumxd.conf] Ubuntu Upstart 
	# service configuration installed by the TokuMX package.  This manages the
	# DATA instance.  Then start the service to initialize its data files.

	cp tokumxd.init.conf /etc/init/tokumxd.conf
	start tokumxd

    # ------------------------------------
    # Configure the CONFIG TokuMX instance.

    # The [tokumxc.conf] file will have been copied to the setup directory by
    # the Cluster Deployer with the specific settings for the VM and deployment.  
    # We'll copy this to the proper directory.

    cp tokumxc.conf /etc/tokumxc.conf

    # The [tokumxc.init.conf] file will have been copied to the setup directory
    # by the Cluster Deployer.  This is the Ubuntu Upstart configuration file.  
    # We're going to copy it to the [/etc/init] folder as [tokumxc.conf] where
    # Upstart  can find it, create a backwards compatible symbolic link and then
    # start the service.

    cp tokumxc.init.conf /etc/init/tokumxc.conf
    ln -s /lib/init/upstart-job /etc/init.d/tokumxc

    initctl reload-configuration
    start tokumxc
	sleep 15		# Give [tokumxc] time to initialize.

    # Create the root user for the CONFIG instance.

    . ./setup-mongo-admin.sh 27019

	# ------------------------------------
	# Configure a log processing CRON job to run [log-processor.sh] every 15
	# minutes.  The current implementation simply deletes TokuMX related
	# log files to prevent them from filling up the disk (which has happened).
	# Eventually, we'll want to upload logs to Elasticsearch (or something)
	# every minute or so.

	cp log-processor.sh /usr/local/sbin
	cat <(crontab -l) <(echo "*/15 * * * * sudo bash /usr/local/sbin/log-processor.sh") | sudo crontab -

    # Indicate that we've successfully installed TokuMX (server).

    echo CONFIGURED > $SETUP_DIR/configured/tokumxd

fi

# The Cluster Deployer should reboot this machine to ensure that all changes 
# have been loaded. 

echo
echo "**********************************************"
echo "**              REBOOT NOW!                 **"
echo "**********************************************"

exit 0
