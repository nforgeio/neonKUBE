#------------------------------------------------------------------------------
# Configure and mount the node disk drives.
#
# This script may reference setup macro varables that look like $<name>.  These 
# will be replaced with node specific values before the script is uploaded to 
# the node.  This is the variable consumed by this script:
#
#   $<data.disk>    The Linux device to be mounted for persistent data 
#                   like [/dev/sdb] or [PRIMARY] if the VM is to use the 
#                   VM's parimary OS disk only.
#
# NOTE: This script must be run under sudo.

DATA_DISK=$<data.disk>

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
# Creates a partition filling the specified drive.

function partitionDrive {

    fdisk $1 << EOF
n
p
1


w
EOF
}

#------------------------------------------------------------------------------

echo "DATA DISK: $DATA_DISK"

if [ "$DATA_DISK" == "PRIMARY" ]; then

	# We have no mounted drives so we're simply going to create a [/mnt-data]
	# folder on the OS drive.

	mkdir -p /mnt-data

else

    # We only have a single data disk so there's no need to configure
    # RAID.  We'll simply initialize and mount the disk.

    # Create the disk partition.

    partitionDrive $DATA_DISK
    PARTITION="${DATA_DISK}1"

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
    # out the line for the new device [/dev/sdc1] or [/dev/md127.  Then 
    # we'll use Bash REGEX to extract the UUID.  Note the device 
    # listing lines look like:
    #
    #	/dev/sdc1: UUID="3d70d51a-fd8a-4761-b36d-dba5ca889b72" TYPE="ext4"
    #
    # or 
    #
    #	/dev/md127: UUID="3d70d51a-fd8a-4761-b36d-dba5ca889b72" TYPE="ext4"
    #
    # depending on whether we detected a single or multiple disks.

    BLOCKID=$(sudo -i blkid | grep $DATA_DEVICE)
    [[ "$BLOCKID" =~ UUID=\"(.*)\"\ TYPE= ]] && UUID=${BASH_REMATCH[1]}

    # Update [/etc/fstab] to ensure that the new drive is mounted after reboots.

    echo UUID=$UUID /mnt-data ext4 defaults,noatime,barrier=0 0 2 | tee -a /etc/fstab
fi

# Indicate that the script has completed.

endsetup disk
