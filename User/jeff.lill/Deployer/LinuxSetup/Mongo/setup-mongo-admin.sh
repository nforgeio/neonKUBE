#------------------------------------------------------------------------------
# Configures the root admin user for a MongoDB instance on the specified port.
#
# Usage:	setup-mongo-admin.sh <port>
#
# Note that the Cluster Deployer will replace the following macros.
#
#		$(adminUser)			- The admin user name
#		$(adminPassword)		- The admin password

mongo localhost:$1 << EOF
use admin
db.createUser(
  {
    user: "$(adminUser)",
    pwd: "$(adminPassword)",
    roles: [ { role: "root", db: "admin" } ]
  })
exit
EOF

# Seems like we need to give this some time to take effect? 

sleep 10