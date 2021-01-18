#------------------------------------------------------------------------------
# Configure and mount the node disk drives.
#
# ARGUMENTS:
#
#       DATA_DISK       - This will be passed as "PRIMARY" when there's no
#                         data disk and node will use the OS disk or the
#                         name of the uninitialized data disk devicce,
#                         like "/dev/sda".
#
#       PARTITION       - This will be passed as the name of the partition
#                         that will be created for the disk.
#
# NOTE: This script must be run under sudo.

DATA_DISK=${1}
PARTITION=${2}

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-DISK                               **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup disk

echo
echo "**********************************************"
echo "** Initializing data disk(s)                **"
echo "**********************************************"

#------------------------------------------------------------------------------
# Creates a disk partition, filling the specified drive.

function partitionDisk {

    fdisk --wipe-partitions always $1 << EOF
n
p
1


w
EOF

    # Sleep a bit to allow the change to persist.  I believe something
    # fancy is happening on AWS that requires this.

    sleep 10
}

#------------------------------------------------------------------------------

echo "DATA DISK: $DATA_DISK"
echo "PARTITION: $PARTITION"

if [ "$DATA_DISK" == "PRIMARY" ]; then

	# We have no mounted drives so we're simply going to create a [/mnt-data]
	# folder on the OS drive.

	mkdir -p /mnt-data

else

    # We only support a single data disk so we'll simply create a partition,
    # initialize an EXT4 file system and mount it.

    # Create the disk partition.

    partitionDisk $DATA_DISK

    # Create an EXT4 file system on the new partition.

    mkfs -t ext4 $PARTITION

    # Mount the file system at [/mnt-data]

    mkdir -p /mnt-data
    mount $PARTITION /mnt-data

    # Remember the data device so we can add it to [/etc/fstab] below.

    DATA_DEVICE=$PARTITION
fi

if [ "$DATA_DISK" != "PRIMARY" ]; then

    # The new file system won't be mounted automatically after a reboot
    # until we add an entry for it in [/etc/fstab].  This is a two
    # step process.  First, we need to get the UUID assigned to the 
    # new file system and then we need to update [/etc/fstab].
    #
    # We're going to do this by listing the device UUIDs and GREPing
    # out the line for the new device.  Then we'll use Bash REGEX to 
    # extract the UUID.  Note the device listing lines look like:
    #
    #	/dev/sdc1: UUID="3d70d51a-fd8a-4761-b36d-dba5ca889b72" TYPE="ext4"

    BLOCKID=$(sudo -i blkid | grep $DATA_DEVICE)
    [[ "$BLOCKID" =~ UUID=\"(.*)\"\ TYPE= ]] && UUID=${BASH_REMATCH[1]}

    # Update [/etc/fstab] to ensure that the new drive is mounted after a reboot.

    echo UUID=$UUID /mnt-data ext4 defaults,noatime,barrier=0 0 2 | tee -a /etc/fstab
fi

# Indicate that the script has completed.

endsetup disk
