#------------------------------------------------------------------------------
# upgrade-shards.sh: Used to manually upgrade the shard config server metadata schema.
#
# NOTE: This script must be run under sudo.

stop mongos
mongos --configdb $(configDBList) --keyFile /etc/mongodb/cluster.key --upgrade --verbose
start mongos

echo
echo "**********************************************"
echo "** Shard metadata upgraded                  **"
echo "**********************************************"
