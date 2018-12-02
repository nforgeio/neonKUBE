# NOTE: 12-03-2018
#
# I've decided to drop support for mounting multiple data disks for cloud 
# environments and then using RAID0 to create a larger disk for both the
# primary VM data area.  This is becoming less necessary as environments
# like Azure will soon support virtual disks of up to 32TB and probably
# beyond in the next few years.
#
# I'm archiving this script in case I want to come back and revisit this
# in the future.

#------------------------------------------------------------------------------
# Configure and mount the node disk drives.  Note that the script currently supports
# 1 to 8 disks with multiple disks being combined into a single RAID0 drive.
#
# NOTE: This script must be run under sudo.

# $todo(jeff.lill):
#
# I need to research whether I need to do additional Linux tuning of the RAID
# chunk size and other SSD related file system parameters.  The article below
# describes some of this.  It's from 2009 though so it may be out of date.
# Hopefully modern Linux distributions automatically tune for SSDs.
#
#		http://blog.nuclex-games.com/2009/12/aligning-an-ssd-on-linux/

# $todo(jeff.lill):
#
# This script is not entirely general purpose.  It will initialize RAID for multiple
# mounted disks on Azure VMs but it doesn't do this for Hyper-V VMs or physical machines.
# One problem is that I'm assuming that the mounted disks will be named:
#
#	/dev/sdc, /dev/sdd, /dev/sde...
#
# which is true on Azure but XenServer mounts these as [/xvd*]...  Hyper-V 
# does mount the disks as [/dev/sd*] but this script will likely conflict
# with disks created for Ceph OSD.
#
# It turns out that by pure luck, this script seems to work OK for now:
#
#	* We don't currently (probably never) need to create a RAID
#     data drive for XenServer and because Xen uses [/dev/xvd*]
#     names, the script below assumes that the machine has just
#	  ephemeral drives.  Any drives created for OSD are ignored.
#
#	* For some reason, Azure mounts the first drive as [/dev/sdc]
#	  instead of [/dev/sdb] so the script below looks to create
#     the RAID array from drives starting at [/dev/sdc]...  This
#     means that any OSD drive mounted in Hyper-V on [/dev/sdb]
#	  won't be inadvertently combined into the RAID array.
#
#	* This may be OK for AWS too, depending on where mounted 
#     EBS drives show up.  
#
# This will need to change though to support OSD on Azure and
# AWS.  The idea is to create two partitions in the RAID array,
# one for the primary file system and the other for OSD.
#
# I'm not entirely sure what I'll do when there are no mounted 
# cloud drives.  Perhaps, I could create a block device in the
# file system and mount OSD there.  I could just require mounted
# cloud drives, but I really don't want to do that now that Ceph
# is enabled by default.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-DISK                               **" 1>&2
echo "**********************************************" 1>&2

# Load the hive configuration and setup utilities.

. $<load-hive-conf>
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

# Detect the number of attached data disks (up to a maximum of 8).

if ls /dev | grep -qF "sdj"
    then DISK_COUNT=8
elif ls /dev | grep -qF "sdi"
    then DISK_COUNT=7
elif ls /dev | grep -qF "sdh"
    then DISK_COUNT=6
elif ls /dev | grep -qF "sdg"
    then DISK_COUNT=5
elif ls /dev | grep -qF "sdf"
    then DISK_COUNT=4
elif ls /dev | grep -qF "sde"
    then DISK_COUNT=3
elif ls /dev | grep -qF "sdd"
    then DISK_COUNT=2
elif ls /dev | grep -qF "sdc"
    then DISK_COUNT=1
else

    # The node has no mounted disks so we're going to configure the local
	# or ephemeral drive instead.  This will be a fast SSD for D, DS and G 
	# series VMs.
        
    DISK_COUNT=0
	mkdir -p /mnt
    ln -s /mnt /mnt-data
fi

echo "DISK COUNT: $DISK_COUNT"

if [ $DISK_COUNT -eq 0 ]; then

	# We have no mounted drives so we're simply going to create a [/mnt-data]
	# folder on the OS drive.

	mkdir -p /mnt-data

elif [ $DISK_COUNT -eq 1 ]; then

    # We only have a single data disk so there's no need to configure
    # RAID.  We'll simply initialize and mount the disk.

    # Create the disk partition.

    partitionDrive /dev/sdc

    # Create an EXT4 file system on the new partition.

    mkfs -t ext4 /dev/sdc1

    # Mount the file system at [/mnt-data]

    mkdir -p /mnt-data
    mount /dev/sdc1 /mnt-data

    # Remember the data device so we can add it to [/etc/fstab] below.

    DATA_DEVICE=/dev/sdc1

else

    # We have more than one drive, so we'll need to install the Linux software RAID
    # solution [mdadm] and then configure the disks.  This script was inspired by
    # this article:
    #
    #	https://azure.microsoft.com/en-us/documentation/articles/virtual-machines-linux-configure-raid/
        
    # Install [mdadm]

    safe-apt-get -q -y install mdadm

    # Create a partition on each disk and build up string including all of the
    # drive partitions (which we'll use below to create the RAID array).

	RAID_CHUNK_SIZE_KB=64

    DISK_PARTITIONS=

    for (( DISK=0; DISK<$DISK_COUNT; DISK++))
    do
        case $DISK in
            0 )
                DISK_NAME=/dev/sdc;;
            1 )
                DISK_NAME=/dev/sdd;;
            2 )
                DISK_NAME=/dev/sde;;
            3 )
                DISK_NAME=/dev/sdf;;
            4 )
                DISK_NAME=/dev/sdg;;
            5 )
                DISK_NAME=/dev/sdh;;
            6 )
                DISK_NAME=/dev/sdi;;
            7 )
                DISK_NAME=/dev/sdj;;
        esac

        partitionDrive $DISK_NAME 

        DISK_PARTITIONS+="${DISK_NAME} "
    done

    # Create the RAID0 array.

    mdadm --create /dev/md127 --level 0 --raid-devices $DISK_COUNT --chunk $RAID_CHUNK_SIZE_KB $DISK_PARTITIONS << EOF
y
EOF

    # Create an EXT4 file system on the new array.

    mkfs -t ext4 /dev/md127
        
    # Mount the file system at [/mnt-data]

    mkdir -p /mnt-data
    mount /dev/md127 /mnt-data

    # Remember the data device so we can add it to [/etc/fstab] below.

    DATA_DEVICE=/dev/md127
fi

if [ ! $DISK_COUNT -eq 0 ]; then

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
