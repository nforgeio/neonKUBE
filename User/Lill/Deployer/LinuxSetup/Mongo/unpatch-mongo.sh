#------------------------------------------------------------------------------
# unpatch-mongo.sh: MongoDB patch uninstall script
#
# NOTE: This script must be run under sudo.
#
# This script removes the MongoDB patch binaries and links,
# restoring the operation of the originally installed package.

# Stop the mongo services.

if [ -f /etc/init/mongod.conf ]; then stop mongod; fi
if [ -f /etc/init/mongoc.conf ]; then stop mongoc; fi
if [ -f /etc/init/mongos.conf ]; then stop mongos; fi

# Delete the patch binaries.

if [ -d /usr/bin-patch/mongo ]; then rm -r /usr/bin-patch/mongo; fi

# Restore links to the release binaries.

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

ln -s /usr/bin/mongo		/usr/sbin
ln -s /usr/bin/mongod		/usr/sbin
ln -s /usr/bin/mongodump	/usr/sbin
ln -s /usr/bin/mongoexport	/usr/sbin
ln -s /usr/bin/mongofiles	/usr/sbin
ln -s /usr/bin/mongoimport	/usr/sbin
ln -s /usr/bin/mongooplog	/usr/sbin
ln -s /usr/bin/mongoperf	/usr/sbin
ln -s /usr/bin/mongorestore	/usr/sbin
ln -s /usr/bin/mongos		/usr/sbin
ln -s /usr/bin/mongostat	/usr/sbin
ln -s /usr/bin/mongotop		/usr/sbin

# Restart the services.

if [ -f /etc/init/mongod.conf ]; then start mongod; fi
if [ -f /etc/init/mongoc.conf ]; then start mongoc; fi
if [ -f /etc/init/mongos.conf ]; then start mongos; fi

