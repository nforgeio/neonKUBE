#------------------------------------------------------------------------------
# Configure and mount the Azure drive(s).  Note that the script currently supports
# 1 to 4 disks with multiple disks being combined into a single RAID0 drive.
#
# NOTE: This script assumes that the data drive will be a local ephemeral SSD or
#       an attached Azure Premium Storage disk (SSD backed).  These setting may
#       not work well for local rotating disks or attached Azure Standard Storage
#       disks.
#
# NOTE: This script must be run under sudo.

# $hack(jeff.lill):
#
# This script is not entirely general purpose.  It will initialize RAID for multiple
# mounted disks on Azure VMs but it doesn't do this for Hyper-V VMs or physical machines.

# Creates a partition filling the specified drive.

function partitionDrive {

    fdisk $1 << EOF
n
p
1


w
EOF
}

if [ ! -f $SETUP_DIR/configured/disk ]; then

    echo
    echo "**********************************************"
    echo "** Initializing data disk(s)                **"
    echo "**********************************************"

    # Detect the number of attached data disks (up to a maximum of 4).

    if ls /dev | grep -qF "sdf"
        then DISK_COUNT=4
    elif ls /dev | grep -qF "sde"
        then DISK_COUNT=3
    elif ls /dev | grep -qF "sdd"
        then DISK_COUNT=2
    elif ls /dev | grep -qF "sdc"
        then DISK_COUNT=1
    else

        # The VM has no mounted disks so we're going to configure the local or
        # ephemeral drive instead.  This will be a fast SSD for D, DS and G 
		# series VMs.
        
        DISK_COUNT=0

        ln -s /mnt /mnt-data
        DATA_DEVICE=/dev/$LOCAL_DISK1
    fi

    echo "DISK COUNT: $DISK_COUNT"

    if [ $DISK_COUNT -eq 1 ]; then

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

    elif [ ! $DISK_COUNT -eq 0 ]; then

        # We have more than one drive, so we'll need to install the Linux software
        # RAID solution [mdadm] and then configure the disks.  This script was adapted
        # from this article:
        #
        #	https://azure.microsoft.com/en-us/documentation/articles/virtual-machines-linux-configure-raid/
        
        # Install [mdadm]

        apt-get -q -y install mdadm

        # Create a partition on each disk and build up string including all of the
        # drive partitions (which we'll use below to create the RAID array).

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
        # we'll use Bash REGEX to extract the UUID.  Note the the device 
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

    # Create the [/usr/local/sbin/tune-disks.sh] script to be called by the 
    # database startup scripts to ensure that the ephemeral and attached
    # disks are tuned for SSDs.

    echo "# This script is generated during service deployment by [setup-disk.sh] to"  > /usr/local/sbin/tune-disks.sh
    echo "# execute the commands necessary to properly tune any attached SSDs.  This" >> /usr/local/sbin/tune-disks.sh
    echo "# will be executed by the the service's [init] or [init.d] script before"   >> /usr/local/sbin/tune-disks.sh
    echo "# the service process is launched."                                         >> /usr/local/sbin/tune-disks.sh
    echo " "                                                                          >> /usr/local/sbin/tune-disks.sh

    if [ "$LOCAL_SSD" = "true" ]; then

        echo "# Configure the local/ephemeral SSD"                                    >> /usr/local/sbin/tune-disks.sh
        echo " "                                                                      >> /usr/local/sbin/tune-disks.sh

        echo "echo noop > /sys/block/$LOCAL_DISK/queue/scheduler"					  >> /usr/local/sbin/tune-disks.sh
        echo "echo 0 > /sys/block/$LOCAL_DISK/queue/rotational"						  >> /usr/local/sbin/tune-disks.sh
        echo "echo $READ_AHEAD_SIZE_KB > /sys/block/$LOCAL_DISK/queue/read_ahead_kb"  >> /usr/local/sbin/tune-disks.sh

	else
		echo "# No SSD: Tuning not required."                                         >> /usr/local/sbin/tune-disks.sh

    fi

    if [ ! $DISK_COUNT -eq 0 ]; then

        echo " "                                                                      >> /usr/local/sbin/tune-disks.sh
        echo "# Configure the attached Azure Premium disk(s)"                         >> /usr/local/sbin/tune-disks.sh
    fi

    if [ $DISK_COUNT -ge 1 ]; then

        echo " "                                                                      >> /usr/local/sbin/tune-disks.sh
        echo "echo noop > /sys/block/sdc/queue/scheduler"							  >> /usr/local/sbin/tune-disks.sh
        echo "echo 0 > /sys/block/sdc/queue/rotational"								  >> /usr/local/sbin/tune-disks.sh
        echo "echo $READ_AHEAD_SIZE_KB > /sys/block/sdc/queue/read_ahead_kb"          >> /usr/local/sbin/tune-disks.sh

    fi

    if [ $DISK_COUNT -ge 2 ]; then

        echo " "                                                                      >> /usr/local/sbin/tune-disks.sh
        echo "echo noop > /sys/block/sdd/queue/scheduler"							  >> /usr/local/sbin/tune-disks.sh
        echo "echo 0 > /sys/block/sdd/queue/rotational"								  >> /usr/local/sbin/tune-disks.sh
        echo "echo $READ_AHEAD_SIZE_KB > /sys/block/sdd/queue/read_ahead_kb"          >> /usr/local/sbin/tune-disks.sh

    fi

    if [ $DISK_COUNT -ge 3 ]; then

        echo " "                                                                      >> /usr/local/sbin/tune-disks.sh
        echo "echo noop > /sys/block/sde/queue/scheduler"							  >> /usr/local/sbin/tune-disks.sh
        echo "echo 0 > /sys/block/sde/queue/rotational"								  >> /usr/local/sbin/tune-disks.sh
        echo "echo $READ_AHEAD_SIZE_KB > /sys/block/sde/queue/read_ahead_kb"          >> /usr/local/sbin/tune-disks.sh

    fi

    if [ $DISK_COUNT -ge 4 ]; then

        echo " "                                                                      >> /usr/local/sbin/tune-disks.sh
        echo "echo noop > /sys/block/sdf/queue/scheduler"							  >> /usr/local/sbin/tune-disks.sh
        echo "echo 0 > /sys/block/sdf/queue/rotational"								  >> /usr/local/sbin/tune-disks.sh
        echo "echo $READ_AHEAD_SIZE_KB > /sys/block/sdf/queue/read_ahead_kb"          >> /usr/local/sbin/tune-disks.sh

    fi

    if [ $DISK_COUNT -ge 2 ]; then

        echo " "																	  >> /usr/local/sbin/tune-disks.sh
        echo "# Configure the RAID disk"											  >> /usr/local/sbin/tune-disks.sh
        echo " "																	  >> /usr/local/sbin/tune-disks.sh
        echo "echo 0 > /sys/block/md127/queue/rotational"						      >> /usr/local/sbin/tune-disks.sh
        echo "echo $READ_AHEAD_SIZE_KB > /sys/block/md127/queue/read_ahead_kb"        >> /usr/local/sbin/tune-disks.sh

    fi

    # Indicate that we've successfully completed the disk configuration.

    echo CONFIGURED > $SETUP_DIR/configured/disk

fi
