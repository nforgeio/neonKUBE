#------------------------------------------------------------------------------
# patch-mongo.sh: MongoDB patch uninstall scriptPATCH
#
# NOTE: This script must be run under sudo.
#
# Usage: bash patch-mongo.sh <version>
#
# Upgrade Steps:
#
#	1. Copy this file to the admin's [setup] folder.
#
#	2. Run this script, passing the patch version.
#
# This script downloads and unpacks the unstable Mongo build and extracts
# it to the: [/usr/bin-patch/mongo] folder.  Then it creates links to
# the Mongo executables in [/usr/sbin].  This will cause the patched binaries
# to be executed instead of the original ones because [/usr/sbin] appears
# in the PATH before [/usr/bin], where the stable Mongo package is installed.
#
# Use the [unpatch-mongo.sh] script to reverse this.

PATCH_VERSION=$1

if [ ! -f $SETUP_DIR/configured/mongo-patch ]; then

	# Download and extract the upgrade binaries.

	curl -O https://fastdl.mongodb.org/linux/mongodb-linux-x86_64-$PATCH_VERSION.tgz
	tar -zxvf mongodb-linux-x86_64-$PATCH_VERSION.tgz

	# Stop the mongo services.

	if [ -f /etc/init/mongod.conf ]; then stop mongod; fi
	if [ -f /etc/init/mongoc.conf ]; then stop mongoc; fi
	if [ -f /etc/init/mongos.conf ]; then stop mongos; fi

	# Delete any old patch binaries and then copy the new ones.

	if [ -d /usr/bin-patch/mongo ]; then rm -r /usr/bin-patch/mongo; fi

	mkdir -p /usr/bin-patch/mongo
	cp -r mongodb-linux-x86_64-$PATCH_VERSION/* /usr/bin-patch/mongo/

	# Create links to the patch binaries.

	rm /usr/sbin/mongo
	rm /usr/sbin/mongod
	rm /usr/sbin/mongodump
	rm /usr/sbin/mongoexport
	rm /usr/sbin/mongofiles
	rm /usr/sbin/mongoimport
	rm /usr/sbin/mongooplog
	rm /usr/sbin/mongoperf
	rm /usr/sbin/mongorestore
	rm /usr/sbin/mongos
	rm /usr/sbin/mongostat
	rm /usr/sbin/mongotop

	ln -s /usr/bin-patch/mongo/bin/mongo		/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongod		/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongodump	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongoexport	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongofiles	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongoimport	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongooplog	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongoperf	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongorestore	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongos		/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongostat	/usr/sbin
	ln -s /usr/bin-patch/mongo/bin/mongotop		/usr/sbin

	# Restart the services.

	if [ -f /etc/init/mongod.conf ]; then start mongod; fi
	if [ -f /etc/init/mongoc.conf ]; then start mongoc; fi
	if [ -f /etc/init/mongos.conf ]; then start mongos; fi

	# Cleanup

	rm -r mongodb-linux-x86_64-$PATCH_VERSION
	rm mongodb-linux-x86_64-$PATCH_VERSION.tgz

	# Indicate that the Mongo patch has been installed.

	echo CONFIGURED > $SETUP_DIR/configured/mongo-patch

fi
