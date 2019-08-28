#------------------------------------------------------------------------------
# Configures the machine to be a TokuMX query router (mongos) server.
#
# NOTE: This script must be run under sudo.

# These parameters need to be modified as necessary by the deployment tool 
# (or manually) to customize the configuration for the specific deployment.

TOKUMX_VERSION=2.0.1-1			# The TokuMX version to be installed
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
# Install TokuMX.

if [ ! -f $SETUP_DIR/configured/tokumxs ]; then

    echo
    echo "**********************************************"
    echo "** Installing TokuMX (router)               **"
    echo "**********************************************"

    . ./setup-tokumx.sh

	# The TokuMX package installs the [tokumx.conf] Upstart script.  We're
	# going to stop the service if its running and delete the script to prevent
	# it from starting on boot.  This server will be configured to run only the
	# router service.

	stop tokumx
	rm /etc/tokumx.conf
	rm /etc/init/tokumx.conf
	rm /var/log/tokumx/tokumx.log
	
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
    # Configure TokuMX (router)

    # The [tokumxs.conf] file will have been copied to the setup directory by
    # the Cluster Deployer with the specific settings for the VM and deployment.  
    # We'll copy this to the proper directory.

    cp tokumxs.conf /etc/tokumxs.conf

    # The [tokumxs.init.conf] file will have been copied to the setup directory
    # by the Cluster Deployer.  This is the Ubuntu Upstart configuration file.  We're
    # going to copy it to the [/etc/init] folder as [tokumxs.conf] where Upstart 
    # can find it, create a backwards compatible symbolic link and then start 
    # the service.

    cp tokumxs.init.conf /etc/init/tokumxs.conf
    ln -s /lib/init/upstart-job /etc/init.d/tokumxs

    initctl reload-configuration
    start tokumxs

    # ------------------------------------
    # [tokumxs] seems somewhat fragile out of the box.  For example, it terminates
    # on start if it is not able to contact all of the shard configuration databases
    # rather than continuing to retry until they come online.
    #
    # We're going to setup a [root] CRON job to run the [service-starter.sh] script 
    # every minute to restart [tokumxs] if it's not already running.

    cp service-starter.sh /usr/local/sbin
    cat <(crontab -l) <(echo "* * * * * sudo bash /usr/local/sbin/service-starter.sh tokumxs tokumxs >>/var/log/service-starter.log 2>>/var/log/service-starter.log") | sudo crontab -

	# ------------------------------------
	# Configure a log processing CRON job to run [log-processor.sh] every 15
	# minutes.  The current implementation simply deletes TokuMX related
	# log files to prevent them from filling up the disk (which has happened).
	# Eventually, we'll want to upload logs to Elasticsearch (or something)
	# every minute or so.

	cp log-processor.sh /usr/local/sbin
	cat <(crontab -l) <(echo "*/15 * * * * sudo bash /usr/local/sbin/log-processor.sh") | sudo crontab -

    # Indicate that we've successfully installed TokuMX (router).

    echo CONFIGURED > $SETUP_DIR/configured/tokumxs

fi

# The Cluster Deployer should reboot this machine to ensure that all changes 
# have been loaded. 

echo
echo "**********************************************"
echo "**              REBOOT NOW!                 **"
echo "**********************************************"

exit 0
