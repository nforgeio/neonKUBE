#------------------------------------------------------------------------------
# FILE:         neoncluster.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# IMPORTANT: Be careful to ensure that these values match the corresponding
#            C# definitions.
#
# This script defines various constants that may be useful for neonCLUSTER containers.
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
#   4. Have [docker-entrypoint.sh] run the script like: . \neoncluster.sh

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

# The Advanced Messaging Queue Protocol (AMPQ) port (e.g. RabbitMQ).
export NetworkPorts_AMQP=5672

# RabbitMQ Admin dashboard port.
export NetworkPorts_RabbitMQAdmin=15672

#------------------------------------------------------------------------------
# NeonClusterConst:
#
# Important neonCLUSTER constants.  These must match the definitions in
# [Neon.Cluster.NeonClusterConst].

# The local endpoint exposed by cluster docker instances to be monitored by the 
# [neon-log-metricbeat] container to capture Docker metrics.
export NeonClusterConst_DockerApiInternalEndpoint=tcp://127.0.0.1:${NetworkPorts_Docker}

# Name of the standard cluster public overlay network.
export NeonClusterConst_ClusterPublicNetwork="neon-public"

# Name of the standard cluster private overlay network.
export NeonClusterConst_ClusterPrivateNetwork="-neon-private"

# IP endpoint of the Docker embedded DNS server.
export NeonClusterConst_NeonClusterConst_DockerDnsEndpoint="127.0.0.11:53"

# The name of the reserved Vault transit key.
export NeonClusterConst_VaultTransitKey="neon-transitkey"

# The port exposed by the <b>neon-proxy-public</b> and <b>neon-proxy-private</b>
# HAProxy service that server the proxy statistics pages.
export NeonClusterConst_HAProxyStatsPort=1936

# The relative URI for the HAProxy statistics pages.
export NeonClusterConst_HaProxyStatsUri="/_stats?no-cache"

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
export NeonClusterConst_HAProxyUidFormat="%{+X}o%ci:%cp_%fi:%fp_%Ts_%rt"

#------------------------------------------------------------------------------
# NeonHostPorts:
#
# Defines the Docker host network ports in the [5000-5499] range reserved 
# by neonCLUSTER used by local services, containters and services on the
# ingress betwork.
#
# These must match the definitions in [Neon.Cluster.NeonHostPorts].

# The main neonCLUSTER dashboard.
export NeonHostPorts_Dashboard=5000

# The [neon-log-kibana] (Kibana) log analysis dashboard.
export NeonHostPorts_Kibana=5001

# The HTTP port exposed by the manager [neon-registry-cache] containers.
export NeonHostPorts_RegistryCache=5002

# The [neon-proxy-vault] service port used for routing HTTP traffic to the
# Vault servers running on the manager nodes.
export NeonHostPorts_ProxyVault=5003

# The public HTTP API port exposed by individual [neon-log-esdata-#]>
# Elasticsearch log repository containers.
export NeonHostPorts_LogEsDataHttp=5004

# The TCP port exposed by individual [neon-log-esdata-#] Elasticsearch
# log repository containers for internal inter-node communication.
export NeonHostPorts_LogEsDataTcp=5005

# The UDP port exposed by the <b>neon-log-host</b> containers that receives
# SYSLOG events from the HAProxy based services and perhaps other sources.
export NeonHostPorts_LogHostSysLog=5006

# This port is reserved and must not be assigned to any service.  This is
# currently referenced by the manager load balancer rule for Azure deployments
# and it must not actually host a service.  See the [AzureHostingManager]
# source code for more information.
export NeonHostPorts_ReservedUnused=5099

#----------------------------------------------------------
# Ports [5100-5299] are reserved for the public proxy that routes
# external traffic into the cluster.
#
# [5100-5102] are used to route general purpose HTTP/S traffic
#             to both neonCLUSTER and application services.
#
# [5102-5109] are reserved for internal neonCLUSTER TCP routes.
#
# [5110-5299] are available for use by application services for TCP or
#             HTTP/S traffic.

# The public proxy port range.
export NeonHostPorts_ProxyPublicFirst=5100
export NeonHostPorts_ProxyPublicLast=5299

# The [neon-proxy-public] service port for routing external HTTP
# (aka Internet) requests to services within the cluster.
export NeonHostPorts_ProxyPublicHttp=5100

# The [neon-proxy-public] service port for routing external HTTPS
# (aka Internet) requests to services within the cluster.
export NeonHostPorts_ProxyPublicHttps=5101

# The first [neon-proxy-public] port available for routing custom
# HTTP/S or TCP services.
export NeonHostPorts_ProxyPublicFirstUser=5110

# The first [neon-proxy-public] port available for routing custom
# HTTP/S or TCP services.
export NeonHostPorts_ProxyPublicLastUser=5299

#----------------------------------------------------------
# Ports [5300-5499] are reserved for the private cluster proxy.
#
# [5300-5301] are used to route general purpose HTTP/S traffic
#             to both neonCLUSTER and application services.
#
# [5302-5309] are reserved for internal neonCLUSTER TCP routes.
#
# [5310-5499] are available for use by application services for TCP or
#             HTTP/S traffic.

# The private proxy port range.
export NeonHostPorts_ProxyPrivateFirst=5300
export NeonHostPorts_ProxyPrivateLast=5499

# The [neon-proxy-private] service port for routing internal HTTP traffic.  
# This typically used to load balance traffic to stateful services that
# can't be deployed as Docker swarm mode services.
export NeonHostPorts_ProxyPrivateHttp=5300

# The [neon-proxy-private] service port for routing internal HTTPS traffic.  
# This typically used to load balance traffic to stateful services that
# can't be deployed as Docker swarm mode services.
export NeonHostPorts_ProxyPrivateHttps=5301

# The [neon-proxy-private] service port for routing internal TCP traffic
# to forward log events from the [neon-log-host] containers running on 
# the nodes to the [neon-log-collector] service.
export NeonHostPorts_ProxyPrivateTcpLogCollector=5302

# The <b>neon-proxy-private</b> service port for routing internal HTTP traffic
# to the logging Elasticsearch cluster.
export NeonHostPorts_ProxyPrivateHttpLogEsData=5303

# The first [neon-proxy-private] port available for routing custom
# HTTP/S or TCP services.
export NeonHostPorts_ProxyPrivatecFirstUser=5310

# The first [neon-proxy-private] port available for routing custom
# HTTP/S or TCP services.
export NeonHostPorts_ProxyPrivateLastUser=5499

#------------------------------------------------------------------------------
# NeonSysLogFacility:
#
# Define the local SysLog facilities reserved for neonCLUSTER purposes.  These
# must match the definitions in [Neon.Cluster.NeonSysLogFacility].

# The syslog facility name used for traffic logs from the neonCLUSTER HAProxy based proxy
# services such as [neon-proxy-vault], [neon-proxy-public], and [neon-proxy-private].
# This maps to syslog facility number 23.
export NeonSysLogFacility_ProxyName=local7

# The syslog facility number used for traffic logs from the neonCLUSTER HAProxy based proxy
# services such as [neon-proxy-vault], [neon-proxy-public], and [neon-proxy-private].
export NeonSysLogFacility_ProxyNumbe=23

#------------------------------------------------------------------------------
# NeonHosts:
#
# Defines the DNS host names used by built-in node level applications as well
# as Docker containers and services.

# The base DNS name for the internal cluster node references.  Note that the
# Consul DNS actually resolves these.
export NeonHosts_ClusterNode=node.cluster

# The base DNS name for the internal cluster Docker registry cache instances deployed on the manager nodes.
export NeonHosts_RegistryCache=neon-registry-cache.service.cluster

# The DNS name for the Elasticsearch containers used to store the cluster logs.
#
# These are individual containers that attached to the [neon-private] network,
# forming an Elasticsearch cluster that is deployed behind the cluster's <b>private</b> proxy.  A DNS entry
# is configured in the each Docker node's [hosts] file to reference the node's IP address as well 
# as in the [/etc/neoncluster/env-host] file that may be mounted into Docker containers and services.
#
# HTTP traffic should be directed to the [NeonHostPorts_ProxyPrivateHttpLogEsData] port which will be
# routed to the [neon-proxy-private] service via the Docker ingress network.
export NeonHosts_LogEsData=neon-log-esdata.service.cluster

# The DNS name used to access for the cluster's HashiCorp Consul service.
export NeonHosts_Vault=neon-consul.service.cluster

# The DNS name for the cluster's HashiCorp Vault proxy.
#
# Cluster services access Vault using this host name to take advantage of the [neon-proxy-vault]
# which provides for failover.
#
# This is also the base name for the manager node specific endpoints like
# <manager-name>.neon-vault.service.cluster, which are used by [neon-proxy-vault]
# to check instance health.
export NeonHosts_Vault=neon-vault.service.cluster

#------------------------------------------------------------------------------
# LogSources:
#
# Identifies some common applications that may emit logs to be processed
# by the neonCLUSTER log pipeline.  These must match the definitions in
# [Neon.Cluster.LogSources].

# Many NeonResearch applications emit a common log message format that
# include an optional timestamp, optional log-level, and optional module
# formatted as decribed in the remarks.
export LogSources_NeonCommon=neon-common

# Elasticsearch cluster node.
export LogSources_ElasticSearch=elasticsearch
