#------------------------------------------------------------------------------
# FILE:         neoncluster.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016 by Neon Research, LLC.  All rights reserved.
#
# IMPORTANT: Be careful to ensure that these values match the corresponding
#            C# definitions.
#
# This script defines various constants that may be useful for NeonCluster containers.
# Containers that requires these should:
#
#   1. have their [build.ps1] script copy this file from the 
#      ["$env:NR_ROOT\\Stack\\Docker\\Images"] directory to the build
#      home directory.
#
#   2. Have the Dockerfile copy the file to the container's root folder.
#
#   3. Have Git ignore the copied file.
#
#   4. Have [docker-entrypoint.sh] run the script like: . \neoncluster.sh

#------------------------------------------------------------------------------
# NeonClusterConst:
#
# Important NeonCluster constants.  These must match the definitions in
# [Neon.Cluster.Management.NeonClusterConst].

# IP endpoint of the Docker embedded DNS server.
export NeonClusterConst_DockerDnsEndpoint="127.0.0.11:53"

# The name of the reserved Vault transit key.
export VaultTransitKey="neon-transitkey"

#------------------------------------------------------------------------------
# NeonClusterPorts:
#
# Define the network ports reserved for NeonCluster purposes.  These must match 
# the definitions in [Neon.Cluster.Management.NeonClusterPorts].

#----------------------------------------------------------
# Ports [11000-11009] are reserved for public proxies that route
# external traffic into the cluster.

# The [neon-proxy-public] service port for routing external (aka Internet) 
# requests to services within the cluster.
export NeonClusterPorts_ProxyPublic=11000

#----------------------------------------------------------
# Ports [11010-11019] are reserved for private cluster proxies.

# The [neon-proxy-private] service port for routing internal traffic.  
# This typically used to load balance traffic to stateful services that
# can't be deployed as Docker swarm mode services.
export NeonClusterPorts_ProxyPrivate=11010

# The [neon-proxy-vault] service port used for routing HTTP traffic to the
# Vault servers running on the manager nodes.
export NeonClusterPorts_ProxyVault=11011

#----------------------------------------------------------
# Ports [11020-11029] are reserved for cluster logging related purposes.

# The [neon-log-kibana] (Kibana) HTTP user interface port for cluster
# log analysis.
export NeonClusterPorts_LogKibana=11020

# The public HTTP API port exposed by for individual [neon-log-esdata#]
# Elasticsearch log repository containers.
export NeonClusterPorts_LogEsDataHttp=11021

# The TCP port exposed by individual [neon-log-esdata-#]> Elasticsearch
# log repository containers for internal inter-node communication.
export NeonClusterPorts_LogEsDataTcp=11022

# The TCP/UDP port where the [tdagent-host] service listens for log events.
export NeonClusterPorts_LogTdAgentHostForward=11023

# The HTTP port where the [tdagent-host] service listens for log events.
export NeonClusterPorts_LogTdAgentHostHttp=11024

#------------------------------------------------------------------------------
# NeonSysLogFacility:
#
# Define the local SysLog facilities reserved for NeonCluster purposes.  these
# must match the definitions in [Neon.Cluster.Management.NeonSysLogFacility].

# The [neon-proxy-vault] service SysLog facility.  This service is responsible
# for routing traffic to the cluster's Vault servers.

export NeonSysLogFacility_VaultLB=local7

# The [neon-proxy-public] service SysLog factility.  This service is
# responsible for routing traffic to public facing services.
export NeonSysLogFacility_ProxyPublic=local6

# The [neon-proxy-private] service SysLog facility.  This service is
# responsible for routing traffic to private cluster services.
export NeonSysLogFacility_ProxyPrivate=local5

#------------------------------------------------------------------------------
# HashiCorp Consul keys reserved by NeonCluster.

# The reserved root key.
export NeonConsulKeys_Root=neon

# Parent key for all cluster load balancers.
export NeonConsulKeys_Balancers="${NeonConsulKeys_Root}/balancers";

# The plain-text HAProxy configuration for the <b>neon-log-esdata</b> 
# load balancer service.
export NeonConsulKeys_NeonLogEsDataBalancerConfig="${NeonConsulKeys_Balancers}/neon-log-esdata/config"

#------------------------------------------------------------------------------
# NetworkPort:
#
# Define the common network port numbers.  These must match the definitions in
# [Neon.Stack.Net.NetworkPort].

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
