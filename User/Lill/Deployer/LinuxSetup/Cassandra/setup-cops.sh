# -----------------------------------------------------------------------------
# Install DataStax OpsCenter.
#
# NOTE: This script must be run under sudo.
#
# NOTE: Macros like $(...) will be replaced by the Deployer.

#------------------------------------------------------------------------------
# These parameters need to be modified as necessary by the deployment tool 
# (or manually) to customize the configuration for the specific deployment.

CASSANDRA_VERSION=2.2.0-1		# The Cassandra version to be installed
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
# Install and configure DataStax OpsCenter.

if [ ! -f $SETUP_DIR/configured/opscenter ]; then

    echo
    echo "**********************************************"
    echo "** Installing Cassandra OpsCenter           **"
    echo "**********************************************"

	# Install Cassandra OpsCenter.  A description of what we're 
	# doing can be found here:
	#
	#	http://docs.datastax.com/en/opscenter/5.2//opsc/install/opscInstallDeb_t.html

	echo "deb http://debian.datastax.com/community stable main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list
	curl -L http://debian.datastax.com/debian/repo_key | sudo apt-key add -

	apt-get update
	apt-get -y -q install opscenter

	# Initialize the configuration but don't start the service.  The Deployer will
	# start it after the data ring has been configured.

	cp opscenterd.conf /etc/opscenter

	mkdir -p /etc/opscenter/clusters
	cp opscenter_cluster.conf /etc/opscenter/clusters/$(clusterName).conf
	
    # Indicate that we've successfully installed and configured Cassandra OpsCenter.

    echo CONFIGURED > $SETUP_DIR/configured/opscenter

fi

# The Cluster Deployer should reboot this machine to ensure that all changes 
# have been loaded. 

echo
echo "**********************************************"
echo "**              REBOOT NOW!                 **"
echo "**********************************************"

exit 0
