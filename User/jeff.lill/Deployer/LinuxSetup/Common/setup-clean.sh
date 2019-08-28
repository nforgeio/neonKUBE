#------------------------------------------------------------------------------
# Performs post setup cleanup
#
# usage: bash setup-clean <setup-folder-path>

echo
echo "**********************************************"
echo "**                Cleanup                   **"
echo "**********************************************"

cd $1

# Delete setup files holding security credentials.

rm -f cluster.key
rm -f setup-mongo-admin.sh
rm -f setup-toku-admin.sh
rm -f setup-shard.sh
rm -f setup-router.sh
rm -f setup-nginx-passwd.sh
