# -----------------------------------------------------------------------------
# Install TokuMX.
#
# NOTE: This script must be run under sudo.

# A description of what we're doing can be found here:
#
#	http://docs.tokutek.com/tokumx/tokumx-installation-packages.html

# Uninstall the default MongoDB packages.

apt-get -q -y --force-yes remove mongodb-org-shell
apt-get -q -y --force-yes remove mongodb-org-tools
apt-get -q -y --force-yes remove mongodb-org-server
apt-get -q -y --force-yes remove mongodb-org-mongos
apt-get -q -y --force-yes remove mongodb-org

# Download and install the package.

apt-key adv --keyserver keyserver.ubuntu.com --recv-key 505A7412
echo "deb [arch=amd64] http://s3.amazonaws.com/tokumx-debs $(lsb_release -cs) main" | tee /etc/apt/sources.list.d/tokumx.list

apt-get -q update
apt-get -q -y install tokumx=$TOKUMX_VERSION

