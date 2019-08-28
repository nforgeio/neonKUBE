#------------------------------------------------------------------------------
# setup-nginx-passwd.sh: Generates the Nngix password file.
#
# NOTE: Macros like $(...) will be replaced by the deployer.

passwordFile=/etc/nginx/kibana.htpasswd
htpasswd -bmc $passwordFile "$(adminUser)" "$(adminPassword)"
htpasswd -bm $passwordFile "$(kibanaUser)" "$(kibanaPassword)"
chown nginx:nginx $passwordFile

passwordFile=/etc/nginx/elastic.htpasswd
htpasswd -bmc $passwordFile "$(adminUser)" "$(adminPassword)"
htpasswd -bm $passwordFile "$(elasticUser)" "$(elasticPassword)"
chown nginx:nginx $passwordFile
