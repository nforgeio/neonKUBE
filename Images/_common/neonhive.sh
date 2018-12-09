#------------------------------------------------------------------------------
# FILE:         neonhive.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# IMPORTANT: Be careful to ensure that these values match the corresponding
#            C# definitions.
#
# This script defines various constants that may be useful for neonHIVE containers.
# Containers that requires these should:
#
#   1. have their [build.ps1] script copy this file from the 
#      ["$env:NF_ROOT\\Stack\\Docker\\Images"] directory to the build
#      home directory.
#
#   2. Have the Dockerfile copy the file to the container's root folder.
#
#   3. Have Git ignore the copied file.
#
#   4. Have [docker-entrypoint.sh] run the script like: . \neonhive.sh

#------------------------------------------------------------------------------
# NetworkPort:
#
# Define the common network port numbers.  These must match the definitions in
# [Neon.Net.NetworkPort].

# HyperText Transport Protocol.
export NetworkPorts_HTTP=80

# Secure HyperText Transport Protocol.
export NetworkPorts_HTTPS=443

# Secure Socket Layer.
export NetworkPorts_SSL=443

# Domain Name System.
export NetworkPorts_DNS=53

# Simple Message Transport Protocol.
export NetworkPorts_SMTP=25

# Post Office Protocol version 3.
export NetworkPorts_POP3=110

# Remote terminal protocol.
export NetworkPorts_TELNET=23

# File Transfer Protocol (control).
export NetworkPorts_FTP=21

# File Transfer Protocol (data).
export NetworkPorts_FTPDATA=20

# Secure File Transfer Protocol.
export NetworkPorts_SFTP=22

# RADIUS authentication and billing protocol.
export NetworkPorts_RADIUS=1812

# Authentication, Authorization, and Accounting.  This port was
# originally used by the RADIUS protocol and is still used
# fairly widely.
export NetworkPorts_AAA=1645

# PING.
export NetworkPorts_ECHO=7

# Daytime (RFC 867).
export NetworkPorts_DAYTIME=13

# Trivial File Transfer Protocol.
export NetworkPorts_TFTP=69

# Secure Shell.
export NetworkPorts_SSH=22

# TIME protocol.
export NetworkPorts_TIME=37

# Network Time Protocol.
export NetworkPorts_NTP=123

# Internet Message Access Protocol.
export NetworkPorts_IMAP=143

# Simple Network Managenment Protocol.
export NetworkPorts_SNMP=161

# Simple Network Managenment Protocol (trap).
export NetworkPorts_SNMPTRAP=162

# Lightweight Directory Access Protocol.
export NetworkPorts_LDAP=389

# Lightweight Directory Access Protocol over TLS/SSL.
export NetworkPorts_LDAPS=636

# Session Initiation Protocol.
export NetworkPorts_SIP=5060

# Secure Session Initiation Protocol (over TLS).
export NetworkPorts_SIPS=5061

# The default port for the <a href="http://en.wikipedia.org/wiki/Squid_%28software%29">Squid</a>
# open source proxy project.
export NetworkPorts_SQUID=3128

# The SOCKS (Socket Secure).
export NetworkPorts_SOCKS=1080

# The HashiCorp Consul.
export NetworkPorts_Consul=8500

# The HashiCorp Vault.
export NetworkPorts_Vault=8200

# The Docker API.
export NetworkPorts_Docker=2375

# The Docker Swarm node advertise.
export NetworkPorts_DockerSwarm=2377

# The Etcd API port.
export NetworkPorts_Etcd=2379

# The internal Etcd cluster peer API port.
export NetworkPorts_EtcdPeer=2380

# The Treasure Data [td-agent] [forward] port 
# to accept TCP and UDP traffic.
export NetworkPorts_TDAgentForward=24224

# The Treasure Data [td-agent] [HTTP] port.
export NetworkPorts_TDAgentHttp=9880

# The ElasticSearch client HTTP port.
export NetworkPorts_ElasticSearchHttp=9200

# The ElasticSearch client TCP port.
export NetworkPorts_ElasticSearchTcp=9300

# The Kibana website port.
export NetworkPorts_Kibana=5601

# The SysLog UDP port.
export NetworkPorts_SysLog=514

# The Couchbase Server web administration user interface port.
export NetworkPorts_CouchbaseWebAdmin=8091

# The Couchbase Server REST API port.
export NetworkPorts_CouchbaseApi=8092

# The Couchbase Sync Gateway administration REST API port.
export NetworkPorts_CouchbaseSyncGatewayAdmin=4985

# The Advanced Messaging Queue Protocol (AMQP) port (e.g. RabbitMQ).
export NetworkPorts_AMQP=5672

# RabbitMQ Admin dashboard port.
export NetworkPorts_RabbitMQAdmin=15672

# Default port for the Ceph dashboard.
export NetworkPorts_CephDashboard=7000

#------------------------------------------------------------------------------
# HiveConst:
#
# Important neonHIVE constants.  These must match the definitions in
# [Neon.Hive.NeonHiveConst].

# Name of the standard hive public overlay network.
export HiveConst_PublicNetwork="neon-public"

# Name of the standard hive private overlay network.
export HiveConst_PrivateNetwork="neon-private"

# IP endpoint of the Docker embedded DNS server.
export HiveConst_DockerDnsEndpoint="127.0.0.11:53"

# The name of the reserved Vault transit key.
export HiveConst_VaultTransitKey="neon-transitkey"

# The port exposed by the [neon-proxy-public] and [neon-proxy-private]
# HAProxy service that server the proxy statistics pages.
export HiveConst_HAProxyStatsPort=1936

# The relative URI for the HAProxy statistics pages.
export HiveConst_HaProxyStatsUri="/_stats?no-cache"

# The HAProxy unique ID generating format.  The generated 
# activity ID parts are:
#
#   %ci:    client IP
#   %cp:    client port
#   %fi:    proxy frontend IP
#   %fp:    proxy frontend port
#   %Ts:    timestamp
#   %rt:    request counter
#
export HiveConst_HAProxyUidFormat="%{+X}o%ci-%cp-%fi-%fp-%Ts-%rt"

# The default username for component dashboards and management tools (like Ceph and RabbitMQ).
export HiveConst_DefaultUsername=sysadmin

# The default password for component dashboards and management tools (like Ceph and RabbitMQ).
export HiveConst_DefaultPassword=password

#------------------------------------------------------------------------------
# HiveHostFolders:
#
# Defines the Docker host network ports in the [5000-5499] range reserved 
# by neonHIVE used by local services, containters and services on the
# ingress network.
#
# These must match the definitions in [Neon.Hive.HiveHostFolders].

# Path to the neonHIVE archive directory.
export HiveHostFolders_Archive=${HOME}/.archive

# Path to the neonHIVE executable files directory.
export HiveHostFolders_Bin=/lib/neon/bin

# Path to the neonHIVE configuration directory.
export HiveHostFolders_Config=/etc/neon

# The folder where hive tools can upload, unpack, and then
# execute <see cref="CommandBundle"/>s as well as store temporary
# command output files.
export HiveHostFolders_Exec=${HOME}/.exec

# Path to the neonHIVE management scripts directory.
export HiveHostFolders_Scripts=/lib/neon/scripts

# Path to the neonHIVE secrets directory.
export HiveHostFolders_Secrets=${HOME}/.secrets

# Path to the neonHIVE setup scripts directory.
export HiveHostFolders_Setup=/lib/neon/setup

# Path to the neonHIVE source code directory.
export HiveHostFolders_Source=/lib/neon/src

# Path to the neonHIVE setup state directory.
export HiveHostFolders_State=/var/local/neon

# Root folder on the local tmpfs (shared memory) folder where 
# neonHIVE will persist misc temporary files.
export HiveHostFolders_Tmpfs=/dev/shm/neon

# Path to the neonHIVE tools directory.
export HiveHostFolders_Tools=/lib/neon/tools

#------------------------------------------------------------------------------
# HiveHostPorts:
#
# Defines the neonHIVE host network ports in the [5000-5499] range reserved 
# for local services, containters and services on the ingress network.
#
# These must match the definitions in [Neon.Hive.HiveHostPorts].

# The local hive Docker registry port.
export HiveHostPorts_DockerRegistryLocal=5000;

# The HTTP port exposed by the manager [neon-registry-cache] containers.
export HiveHostPorts_DockerRegistryCache=5001

# The main neonHIVE dashboard.
export HiveHostPorts_Dashboard=5002

# The [neon-log-kibana] (Kibana) log analysis dashboard.
export HiveHostPorts_Kibana=5003

# The [neon-proxy-vault] service port used for routing HTTP traffic to the
# Vault servers running on the manager nodes.
export HiveHostPorts_ProxyVault=5004

# The public HTTP API port exposed by individual [neon-log-esdata-#]>
# Elasticsearch log repository containers.
export HiveHostPorts_LogEsDataHttp=5005

# The TCP port exposed by individual [neon-log-esdata-#] Elasticsearch
# log repository containers for internal inter-node communication.
export HiveHostPorts_LogEsDataTcp=5006

# The UDP port exposed by the [neon-log-host] containers that receives
# SYSLOG events from the HAProxy based services and perhaps other sources.
export HiveHostPorts_LogHostSysLog=5007

# The port exposed by the hive's Ceph dashboard.
export HiveHostPorts_CephDashboard=5008

# The RabbitMQ/Erlang peer discovery protocol port.
export HiveHostPorts_HiveMQEPMD=5009

# The RabbitMQ message broker AMQP port.
export HiveHostPorts_HiveMQAMQP=5010

# The RabbitMQ message broker cluster internal communication port.
export HiveHostPorts_HiveMQDIST=5011

# The RabbitMQ management dashboard port.
export HiveHostPorts_HiveMQManagement=5012

# This port is reserved and must not be assigned to any service.  This is
# currently referenced by the manager traffic manager rule for Azure deployments
# and it must not actually host a service.  See the [AzureHostingManager]
# source code for more information.
export HiveHostPorts_ReservedUnused=5099

#----------------------------------------------------------
# Ports [5100-5299] are reserved for the public proxy that routes
# external traffic into the hive.
#
# [80/443]    are used to route general purpose HTTP/S traffic
#             to both neonHIVE and application services.
#
# [5100-5109] are reserved for internal neonHIVE TCP routes.
#
# [5120-5299] are available for use by application services for TCP or
#             specialized HTTP/S traffic.

# The public proxy port range.
export HiveHostPorts_ProxyPublicFirst=5100
export HiveHostPorts_ProxyPublicLast=5299

# The [neon-proxy-public] service port for routing external HTTP
# (aka Internet) requests to services within the hive.
export HiveHostPorts_ProxyPublicHttp=80

# The [neon-proxy-public] service port for routing external HTTPS
# (aka Internet) requests to services within the hive.
export HiveHostPorts_ProxyPublicHttps=443

# The first [neon-proxy-public] port available for routing custom
# HTTP/S or TCP services.
export HiveHostPorts_ProxyPublicFirstUser=5120

# The first [neon-proxy-public] port available for routing custom
# HTTP/S or TCP services.
export HiveHostPorts_ProxyPublicLastUser=${HiveHostPorts_ProxyPublicLast}

#----------------------------------------------------------
# Ports [5300-5499] are reserved for the private hive proxy.
#
# [5300-5301] are used to route general purpose HTTP/S traffic
#             to both neonHIVE and application services.
#
# [5302-5309] are reserved for internal neonHIVE TCP routes.
#
# [5320-5499] are available for use by application services for TCP or
#             HTTP/S traffic.

# The private proxy port range.
export HiveHostPorts_ProxyPrivateFirst=5300
export HiveHostPorts_ProxyPrivateLast=5499

# The [neon-proxy-private] service port for routing internal HTTP traffic.  
# This typically used to load balance traffic to stateful services that
# can't be deployed as Docker swarm mode services.
export HiveHostPorts_ProxyPrivateHttp=5300

# The [neon-proxy-private] service port for routing internal HTTPS traffic.  
# This typically used to load balance traffic to stateful services that
# can't be deployed as Docker swarm mode services.
export HiveHostPorts_ProxyPrivateHttps=5301

# The [neon-proxy-private] service port for routing internal TCP traffic
# to forward log events from the [neon-log-host] containers running on 
# the nodes to the [neon-log-collector] service.
export HiveHostPorts_ProxyPrivateTcpLogCollector=5302

# The [neon-proxy-private] service port for routing internal HTTP traffic
# to the logging Elasticsearch cluster.
export HiveHostPorts_ProxyPrivateHttpLogEsData=5303

# The port assigned to the hive Ceph web dashboard.
export HiveHostPorts_ProxyPrivateHttpCephDashboard=5304

# The port assigned to the load balanced Kibana dashboard.
export HiveHostPorts_ProxyPrivateKibanaDashboard=5305

# The port assigned to the load balanced HiveMQ AMQP endpoint.
export HiveHostPorts_ProxyPrivateHiveMQAMQP=5306

# The port assigned to the load balanced HiveMQ management 
# plugin.  This serves the management REST API as well as
# the dashboard.
export HiveHostPorts_ProxyPrivateHiveMQManagement=5307

# The first [neon-proxy-private] port available for routing custom
# HTTP/S or TCP services.
export HiveHostPorts_ProxyPrivateFirstUser=5320

# The first [neon-proxy-private] port available for routing custom
# HTTP/S or TCP services.
export HiveHostPorts_ProxyPrivateLastUser=${HiveHostPorts_ProxyPrivateLast}

#------------------------------------------------------------------------------
# HiveSysLogFacility:
#
# Define the local SysLog facilities reserved for neonHIVE purposes.  These
# must match the definitions in [Neon.Hive.NeonSysLogFacility].

# The syslog facility name used for traffic logs from the neonHIVE HAProxy based proxy
# services such as [neon-proxy-vault], [neon-proxy-public], and [neon-proxy-private].
# This maps to syslog facility number 23.
export HiveSysLogFacility_ProxyName=local7

# The syslog facility number used for traffic logs from the neonHIVE HAProxy based proxy
# services such as [neon-proxy-vault], [neon-proxy-public], and [neon-proxy-private].
export HiveSysLogFacility_ProxyNumbe=23

#------------------------------------------------------------------------------
# Identifies the hive Consul globals and settings.  These are located
# under [neon/global].

# Enables unit testing on the hive via <b>HiveFixture</b> (bool).
export HiveGlobals_AllowUnitTesting=allow-unit-testing

# Hive creation date (UTC).
export HiveGlobals_CreateDateUtc=create-date-utc

# Current hive definition as compressed JSON.
export HiveGlobals_DefinitionDeflate=definition-deflated

# MD5 hash of the current hive definition.
export HiveGlobals_DefinitionHash=definition-hash

# Disables automatic Vault unsealing (bool).
export HiveGlobals_DisableAutoUnseal=disable-auto-unseal

# Specifies the number of days to retain [logstash] and
# [metricbeat] logs.
export HiveGlobals_LogRetentionDays=log-retention-days

# Minimum <b>neon-cli</b> version allowed to manage the hive.
export HiveGlobals_NeonCli=neon-cli

# Current hive pets definition.
export HiveGlobals_PetsDefinition=pets-definition

# Hive globally unique ID assigned during hive setup.
export HiveGlobals_Uuid=uuid

# Version of the hive.  This is actually the version of [neon-cli] 
# that created or last upgraded the hive.
export HiveGlobals_Version=version

#------------------------------------------------------------------------------
# LogSources:
#
# Identifies some common applications that may emit logs to be processed
# by the neonHIVE log pipeline.  These must match the definitions in
# [Neon.Hive.LogSources].

# Many NeonResearch applications emit a common log message format that
# include an optional timestamp, optional log-level, and optional module
# formatted as decribed in the remarks.
export LogSources_NeonCommon=neon-common

# Elasticsearch cluster node.
export LogSources_ElasticSearch=elasticsearch
