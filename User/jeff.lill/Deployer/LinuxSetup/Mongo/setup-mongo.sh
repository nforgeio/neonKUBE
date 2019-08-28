# -----------------------------------------------------------------------------
# Install MongoDB.
#
# NOTE: This script must be run under sudo.

# A description of what we're doing can be found here:
#
#	http://docs.mongodb.org/manual/tutorial/install-mongodb-on-ubuntu/

# Download and install the package.

apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv 7F0CEB10

echo "deb http://repo.mongodb.org/apt/ubuntu "$(lsb_release -sc)"/mongodb-org/3.0 multiverse" | sudo tee /etc/apt/sources.list.d/mongodb-org-3.0.list

apt-get -q update
apt-get -q -y install mongodb-org=$MONGO_VERSION mongodb-org-server=$MONGO_VERSION mongodb-org-shell=$MONGO_VERSION mongodb-org-mongos=$MONGO_VERSION mongodb-org-tools=$MONGO_VERSION

echo "mongodb-org hold" | sudo dpkg --set-selections
echo "mongodb-org-server hold" | sudo dpkg --set-selections
echo "mongodb-org-shell hold" | sudo dpkg --set-selections
echo "mongodb-org-mongos hold" | sudo dpkg --set-selections
echo "mongodb-org-tools hold" | sudo dpkg --set-selections

# Configure links to the release binaries.  The Upstart configuration files
# reference these links rather than the installed binaries directly so it
# will be possible to patch specific executables without having to modify
# the Upstart configuration.

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

