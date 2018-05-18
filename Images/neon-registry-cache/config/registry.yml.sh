#------------------------------------------------------------------------------
# FILE:         registry.yml.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Generates the Docker registry configuration file [/registry.yml] 
# by substituting environment variables.

cat <<EOF > /registry.yml
version: 0.1
log:
  level: ${LOG_LEVEL}
  formatter: json
  fields:
    service_type: neon-registry-cache
storage:
  filesystem:
    rootdirectory: /var/lib/neon-registry-cache
    maxthreads: 100
  delete:
    enabled: true
  redirect:
    disable: false
  cache:
    blobdescriptor: inmemory
  maintenance:
    uploadpurging:
      enabled: true
      age: 168h
      interval: 24h
      dryrun: false
    readonly:
      enabled: false
http:
  addr: 0.0.0.0:5000
  host: ${HOSTNAME}:5000
  tls:
    certificate: /etc/neon-registry-cache/cache.crt
    key: /etc/neon-registry-cache/cache.key
  headers:
    X-Content-Type-Options: [nosniff]
EOF
