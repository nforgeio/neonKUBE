# -----------------------------------------------------------------------------
# Install the DataStax OpsCenter Agent.
#
# NOTE: This script must be run under sudo.

# A description of what we're doing can be found here:
#
#	http://docs.datastax.com/en/opscenter/5.2//opsc/install/opscManualInstallAgentPkgDeb.html

echo "deb http://debian.datastax.com/community stable main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list
curl -L http://debian.datastax.com/debian/repo_key | sudo apt-key add -

apt-get update
apt-get -y -q install datastax-agent
