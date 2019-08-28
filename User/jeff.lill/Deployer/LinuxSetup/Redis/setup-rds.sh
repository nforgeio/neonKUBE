#------------------------------------------------------------------------------
# Configures the machine to be a Redis node.
#
# NOTE: This script must be run under sudo.

# These parameters need to be modified as necessary by the deployment tool 
# (or manually) to customize the configuration for the specific deployment.

REDIS_VERSION=3.0.3				# The Redis version to be installed
LOGSTASH_VERSION=1:1.5.2-1  	# The Logstash version to be installed
JAVA_VERSION=8					# Can be "7" or "8"
RAID_CHUNK_SIZE_KB=64			# The RAID chunk size in KB
READ_AHEAD_SIZE_KB=0			# The DATA drive read-ahead size in KB (must be a power of 2)
LOCAL_DISK=sda 			        # The local data drive (this will be [sdb] for Azure VMs)
LOCAL_SSD=false                 # The data drive is SSD backed

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
# Machine Configuration

. ./setup-ntp.sh
. ./setup-disk.sh
. ./setup-hosts.sh
. ./setup-java.sh
. ./setup-linux.sh
. ./setup-dotnet.sh
. ./setup-logstash.sh

# -----------------------------------------------------------------------------
# Download and build Redis and its tools.

if [ ! -f $SETUP_DIR/configured/redis ]; then

    echo
    echo "**********************************************"
    echo "** Installing Redis                         **"
    echo "**********************************************"

    # Build Redis.

    . ./setup-redis.sh

    # Copy the config file.

	cp redis.conf /etc

	# Create the directory where REDIS will persist its data.

	mkdir -p /mnt-data/redis

	# Configure and start the service.
	
	cp redis.initd.conf /etc/init.d/redis
	chmod 755 /etc/init.d/redis
	chown root:root /etc/init.d/redis

	update-rc.d -f redis defaults
	service redis start

    # Indicate that we've successfully installed and configured Redis.

    echo CONFIGURED > $SETUP_DIR/configured/redis

fi

# The Cluster Deployer should reboot this machine to ensure that all changes 
# have been loaded. 

echo
echo "**********************************************"
echo "**              REBOOT NOW!                 **"
echo "**********************************************"

exit 0
