# -----------------------------------------------------------------------------
# Download and build Redis.
#
# NOTE: This script must be run under sudo.

# A description of what we're doing can be found here:
#
#	http://redis.io/download and
#	http://sharadchhetri.com/2015/07/05/install-redis-3-0-from-source-on-ubuntu-14-04-centos-7-rhel-7/

apt-get update

# Make sure that the GCC compiler, MAKE, and the standard library development kit are installed.

apt-get -y -q install make
apt-get -y -q install gcc
apt-get -y -q install libc6-dev

# Download the REDIS source code.

curl http://download.redis.io/releases/redis-$REDIS_VERSION.tar.gz > redis-$REDIS_VERSION.tar.gz
tar xzf redis-$REDIS_VERSION.tar.gz
cd redis-$REDIS_VERSION

# Build the REDIS dependencies.

cd $SETUP_DIR/redis-$REDIS_VERSION\deps
make hiredis lua jemalloc linenoise > $SETUP_DIR/make-deps.log

# Build and install REDIS.

cd $SETUP_DIR/redis-$REDIS_VERSION

make          > $SETUP_DIR/make-redis.log
make install  > $SETUP_DIR/make-install.log

# Optional: Some of the REDIS tools require Ruby and the Ruby REDIS client.

# apt-get -q -y install ruby
# gem install redis

# Make sure we're back in the setup directory.

cd $SETUP_DIR
