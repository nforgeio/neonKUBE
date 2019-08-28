#------------------------------------------------------------------------------
# Configures the machine to be a Cassandra database node.
#
# NOTE: This script must be run under sudo.

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
# Machine Configuration

. ./setup-ntp.sh
. ./setup-disk.sh
. ./setup-hosts.sh
. ./setup-java.sh
. ./setup-linux.sh
. ./setup-dotnet.sh
. ./setup-logstash.sh

if [ ! -f $SETUP_DIR/configured/hosts ]; then

    echo
    echo "**********************************************"
    echo "** Initializing HOSTS file                  **"
    echo "**********************************************"

    # We need to add a loopback address mapping for the VM at the top
    # of the [/etc/hosts] file so OpsCenter will label the node properly.

    echo 127.0.0.1 $HOSTNAME > hosts-temp
    cat /etc/hosts >> hosts-temp
    cp hosts-temp /etc/hosts
    rm hosts-temp

    # Indicate that we've successfully completed the host configuration.

    echo CONFIGURED > $SETUP_DIR/configured/hosts
fi

# -----------------------------------------------------------------------------
# Install and configure Cassandra and Tools.

if [ ! -f $SETUP_DIR/configured/cassandra ]; then

    echo
    echo "**********************************************"
    echo "** Installing Cassandra                     **"
    echo "**********************************************"

    # Install Cassandra.

    . ./setup-cassandra.sh

    # Stop Cassandra and update the configuration to the files
	# uploaded by the deployer.  Note that we're not going to 
	# restart Cassandra in this script.  Instead, we're going
	# to have the Deployer reboot the nodes one-by-one so that
	# they will be added to the cluster in a controlled manner.

    if [ -f /etc/cassandra/cassandra-topology.properties ]; then
        rm /etc/cassandra/cassandra-topology.properties
    fi

    # Copy the config files.

    cp cassandra.yaml					/etc/cassandra
    cp cassandra-env.sh					/etc/cassandra
    cp cassandra-rackdc.properties		/etc/cassandra
    cp commitlog_archiving.properties	/etc/cassandra
    cp logback.xml						/etc/cassandra
    cp logback-tools.xml				/etc/cassandra

    # The Upstart script in the project doesn't work for a couple reasons:
    #
    #    * It doesn't appear to shut Cassandra down gracefully such that
    #      files are corrupted after a reboot.  Cassandara won't restart.
    #
    #    * OpsCenter/DataStax-Agent depends in the [init.d] script to
    #      manage the service.
    #
    # We're going to comment this out for now.
    #
    # rm /etc/init.d/cassandra
    # update-rc.d cassandra remove
	#
	# ...and configure a modified [init.d] script instead.

    cp cassandra.initd.conf /etc/init.d/cassandra

	# Technically, we could use the commands below to reconfigure the
	# [rc.d] links and file attributes, but this isn't necessary because
	# we're overwriting a the script installed by the Cassandra package.

	# chmod +x /etc/init.d/cassandra
	# chown root:root /etc/init.d/cassandra
	# update-rc.d -f cassandra remove
	# update-rc.d -f cassandra defaults

    # Initialize the data directories.

    mkdir -p /mnt-data/cassandra
    chown cassandra:cassandra /mnt-data/cassandra

    mkdir -p /mnt-data/cassandra/commitlog
    chown cassandra:cassandra /mnt-data/cassandra/commitlog

    mkdir -p /mnt-data/cassandra/saved_caches
    chown cassandra:cassandra /mnt-data/cassandra/saved_caches

    # Indicate that we've successfully installed and configured Cassandra.

    echo CONFIGURED > $SETUP_DIR/configured/cassandra

fi

# -----------------------------------------------------------------------------
# Install the DataStax OpsCenter Agent

if [ ! -f $SETUP_DIR/configured/agent ]; then

    echo
    echo "**********************************************"
    echo "** Installing OpsCenter DataStax Agent      **"
    echo "**********************************************"

    . ./setup-agent.sh

    # Configure and then start the agent.

    cp address.yaml /etc/datastax-agent
    cp address.yaml /var/lib/datastax-agent/conf
    
    # Indicate that we've successfully installed the agent.

    echo CONFIGURED > $SETUP_DIR/configured/agent

fi

# The Cluster Deployer should reboot this machine to ensure that all changes 
# have been loaded. 

echo
echo "**********************************************"
echo "**              REBOOT NOW!                 **"
echo "**********************************************"

exit 0
