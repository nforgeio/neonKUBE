# -----------------------------------------------------------------------------
# Install Cassandra and related tools.
#
# NOTE: This script must be run under sudo.

# A description of what we're doing can be found here:
#
#	http://docs.datastax.com/en/cassandra/2.2/cassandra/install/installDeb.html

echo "deb http://debian.datastax.com/community stable main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list
curl -L http://debian.datastax.com/debian/repo_key | sudo apt-key add -

apt-get update
apt-get -y -q install dsc22=$CASSANDRA_VERSION
apt-get -y -q install cassandra-tools

# Stop the Cassandra and wipe its default data directory because
# we'll be configuring a new data directory in [/mnt-data].

service cassandra stop
rm -rf /var/lib/cassandra
