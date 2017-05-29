#------------------------------------------------------------------------------
# Configures the root admin user for a TokuMX instance on the specified port.
#
# Usage:	setup-toku-admin.sh <port>
#
# Note that the Cluster Deployer will replace the following macros.
#
#		$(adminUser)			- The admin user name
#		$(adminPassword)		- The admin password
#
# Note that this differs from [setup-mongo-admin.sh] because sometime between
# TokuMX's last merge with the Mongo source and now, Mongo has changed the 
# [db.addUser()] API to [db.createUser()] with parameter changes.
#
# We should be able to unify these sometime in the future.

mongo localhost:$1 << EOF
use admin
db.addUser(
	{ 
		user: "$(adminUser)",
		pwd: "$(adminPassword)",
		roles: [ "clusterAdmin", 
				 "userAdminAnyDatabase",
                 "dbAdminAnyDatabase",
                 "readWriteAnyDatabase" ] 
	})
exit
EOF

# Seems like we need to give this some time to take effect? 

sleep 10
