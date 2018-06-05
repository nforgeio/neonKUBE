//-----------------------------------------------------------------------------
// FILE:	    ClusterExampleCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster example</b> command.
    /// </summary>
    public class ClusterExampleCommand : CommandBase
    {
        private const string usage = @"
Writes a sample cluster definition file to the standard output.  You
can use this as a starting point for creating a customized definition.

USAGE:

    neon cluster example
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "example" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            const string sampleJson =
@"//-----------------------------------------------------------------------------
// This is a sample cluster definition file.  This can be a good starting point 
// for creating a custom cluster.
//
// The file format is JSON with some preprocessing capabilities.  Scroll down to
// the bottom of the file for more information.

//-----------------------------------------------------------------------------
// The JSON below defines a Docker cluster with three manager and four worker
// and one pet node.
//
// Naming: Cluster, datacenter and node names may be one or more characters 
//         including case insensitive letters, numbers, dashes, underscores,
//         or periods.

{
    //  Name              Cluster name
    //  Datacenter        Datacenter name
    //  Environment       Describes the type of environment, one of:
    //
    //                      other, development, test, staging, or production
    //
    //  TimeSources       The FQDNs or IP addresses of the NTP time sources
    //                    used to synchronize the cluster as an array of
    //                    strings.  Reasonable defaults will be used if
    //                    not specified.
    //
    //  PackageProxy      Optionally specifies zero or more comma separated 
    //                    HTTP URL including the port (generally [3142]) of
    //                    the servers to be used for proxying and caching 
    //                    Ubuntu and Debian APT packages.  This defaults to 
    //                    proxies running on the cluster managers if null
    //                    or empty (the default).
    //
    //  BareDocker        Optionally indicates that a basic Docker cluster without
    //                    most of the extra neonCLUSTER features should be deployed.
    //                    This defaults to [false].
    //
    //  AllowUnitTesting  Optionally enable unit testing on this cluster.  
    //                    This defaults to [false]. 
    //
    //  These properties specify the Docker image to be deployed for various
    //  cluster level services.  The reasonable default values for each are
    //  shown on the right.
    //
    //  ProxyImage              neoncluster/neon-proxy:latest
    //  ProxyVaultImage         neoncluster/neon-proxy-vault:latest
    //  ProxyManagerImage       neoncluster/neon-proxy-manager:latest
    //  ClusterManagerImage     neoncluster/neon-cluster-manager:latest

    ""Name"": ""my-cluster"",
    ""Datacenter"": ""Seattle"",
    ""Environment"": ""development"",
    ""TimeSources"": [ ""pool.ntp.org"" ],
    ""PackageProxy"": null,

    // Cluster hosting provider options:

    ""Hosting"": {

        // Identifies the hosting provider.  The possible values are [aws],
        // [azure], [google], [hyper-v], [local-hyper-v], [machine], or
        // [xenserver].  This defaults to [machine].
        //
        // You may need to uncomment and initialize the corresponding section
        // below.  See the documentation for the details.

        ""Provider"": ""machine"",

        // Hosting environment specific options.

        // ""Aws"": { ... }
        // ""Azure"": { ... }
        // ""Google"": { ... }
        // ""HyperV"": { ... }
        // ""LocalHyperV"": { ... }
        // ""Machine"": { ... }
        // ""XenServer"": { ... }

        // The following options are available for on-premise clusters deployed
        // to hypervisor based environments such as [hyper-v], [local-hyper-v],
        // and [xenserver].

        // Describes the hypervisor host machines for [hyper-v] and [xenserver]
        // based environments so that the neonCLUSTER tooling will be able to
        // connect to and manage the hypervisor host machines.  Cluster node
        // definitions will set [node.VmHost=Name] to the name of the host below
        // where the node should be provisioned for these environments.
        //
        // ""VmHosts"": [
        //   {
        //     ""Name"": ""XEN-00"",
        //     ""Address"": ""10.0.0.20"",
        //     ""Username"": ""root"",
        //     ""Password"": ""sysadmin0000""
        //   }
        // ]
    
        // The default hypervisor host username to use when this is not specified
        // explicitly in [VmHosts].
        //
        // ""VmHostUsername"" : ""sysadmin""
    
        // The default hypervisor host password to use when this is not specified
        // explicitly in [VmHosts].
        //
        // ""VmHostPassword"" : ""sysadmin0000""

        // The default number of virtual processors to assign to each cluster virtual 
        // machine.  This defaults to [4].
        //
        // "" VmProcessors"": 4

        // Specifies the default maximum amount of memory to allocate to each cluster 
        // virtual machine.  This is specified as a string that can be a long byte count 
        // or a long with units like [512MB] or [2GB].  This defaults to [4GB].
        //
        // ""VmMemory"": ""4GB""

        // Specifies the minimum amount of memory to allocate to each cluster virtual machine.  This is specified as a string that
        // can be a a long byte count or a long with units like [512MB] or [2GB] or may be set to [null] to set
        // the same value as [VmMemory].  This defaults to [2GB], which is half of the default value of [VmMemory].
        // which is [4GB].
        //
        // This is currently honored only when provisioning to a local Hyper-V instance 
        // (typically as a developer).  This is ignored for XenServer and when provisioning 
        // to remote Hyper-V or XenServer instances.
        //
        // ""VmMinimumMemory"": ""2GB""

        // Specifies the maximum amount of memory to allocate to each cluster virtual machine.
        // This is specified as a string that can be a long byte count or a long with units like 
        // [512MB] or [2GB].  This defaults to[64GB]>.
        // 
        // ""VmDisk"": ""64GB""

        // Path to the folder where virtual machine hard drive folders are to be persisted.
        // This defaults to the local Hyper-V folder for Windows.
        //
        // This is recognized only when deploying on a local Hyper-V hypervisor, typically
        // for development and test poruposes.  This is ignored when provisioning on remote
        // Hyper-V instances or for hypervisors.
        //
        // ""VmDriveFolder"": null

        // The prefix to be prepended to virtual machine provisioned to hypervisors for the
        // <see [hyper-v], [local-hyper-v], and [xenserver] environments.
        // 
        // When this is [null] (the default), the cluster name followed by a dash will prefix the
        // provisioned virtual machine names.  When this is a non-empty string, the value
        // value followed by a dash will be used.  If this is empty or whitespace, machine
        // names will not be prefixed.
        //
        // Virtual machine name prefixes will always be converted to lowercase.
        //
        // ""VmNamePrefix"": null
    },

    // Built-in 

    ""Vpn"": {
    
        // Enables the built-in cluster VPN.  This defaults to [false].

        ""Enabled"": false,

        // Specifies whether the same client certificate can be used to establish more than one connection
        // to the cluster VPN.  This enables a single operator to establish multiple connections (e.g. from
        // different computers) or for operators to share credentials to simplify certificate management.
        // This defaults to [true].
        //
        // Enabling this trades a bit of security for convienence.
        //
        // ""AllowSharedCredentials"": true,

        // Specifies the two-character country code to use for the VPN certificate authority.
        // This defaults to [US].
        //
        // ""CertCountryCode"": ""US"",

        // Specifies the organization name to use for the VPN certificate authority.  This defaults 
        // to the cluster name with [""-cluster""] appended.

        // ""CertOrganization"" : ""my-authority""
    },

    // Cluster host node options:

    ""HostNode"": {

        // Specifies the operating system to be installed on the cluster nodes.
        // This currently defaults to [ubuntu-16.04].

        ""OperatingSystem"": ""ubuntu-16.04"",

        // Specifies whether the host node operating system should be upgraded
        // during cluster preparation.  The possible values are [full], [partial],
        // and [none].  This defaults to [full] to pick up most criticial updates.

        ""Upgrade"": ""full"",

        // Specifies the authentication method to be used to secure SSH sessions
        // to the cluster host nodes.  The possible values are:
        //
        //      password    - username/password
        //      tls         - mutual TLS via public certificates and private keys
        //
        // This defaults to [tls] for better security.

        ""SshAuth"": ""tls"",

        // Cluster hosts are configured with a random root account password.
        // This defaults to [20] characters.  The minumum non-zero length
        // is [8].  Specify [0] to leave the root password unchanged.
        //
        // IMPORTANT: Setting this to zero will leave the cluster open for
        // password authentication in addition to mutual TLS authentication 
        // (if enabled).  Think very carefully before doing this for a 
        // production cluster.

        ""PasswordLength"": 20
    },

    // Docker related options:

    ""Docker"": {

        // The version of Docker to be installed.  This can be an older released Docker version
        // like [1.13.0] or a newer version like [17.03.0-ce].  You can also specify or [latest]
        // to install the most recent production release or specify [test], [experimental] for
        // other common release channels.
        //
        // This defaults to [latest].
        //
        // IMPORTANT!
        //
        // Production clusters should always install a specific version of Docker so 
        // you will be able to add new hosts in the future that will have the same 
        // Docker version as the rest of the cluster.  This also prevents the package
        // manager from inadvertently upgrading Docker.
        //
        // IMPORTANT!
        //
        // It is not possible for the [neon-cli] to upgrade Docker on clusters
        // that deployed the [test] or [experimental] builds.

        ""Version"": ""latest"",

        // Optionally specifies the hostnames and credentials for the Docker registries
        // that will be available to the cluster.  Note that clusters will access the
        // Docker public registry without authentication by default.

        // ""Registry"": [
        //   {
        //     ""HostName"": ""MY-REGISTRY"",
        //     ""Username"": ""MY-USERNAME"",
        //     ""Password"": ""MY-PASSWORD""
        //   }
        // ],
        
        // Optionally specifies that pull-thru registry caches are to be deployed
        // within the cluster on the manager nodes.  This defaults to [true]. 
        
        ""RegistryCache"": true,

        // Optionally enables experimental Docker features.  This defaults to [false].
        
        ""Experimental"": false,        

        // The Docker daemon container logging options.  This defaults to:
        //
        //      --log-driver=fluentd --log-opt tag= --log-opt fluentd-async-connect=true
        // 
        // which by default, will forward container logs to the cluster logging pipeline.
        // 
        // IMPORTANT:
        //
        // Always use the [--log-opt fluentd-async-connect=true] option when using 
        // the [fluentd] log driver.  Containers without this will stop if the 
        // logging pipeline is not ready when the container starts.
        //
        // You may have individual services and containers opt out of cluster logging by 
        // setting [--log-driver=json-text] or [-log-driver=none].  This can be handy 
        // while debugging Docker images.
        
        ""LogOptions"": ""--log-driver=fluentd --log-opt tag= --log-opt fluentd-async-connect=true""
    },

    // Cluster Network options:

    ""Network"": {
        
            //  PremiseSubnet               Specifies the subnet for entire host network for on-premise
            //                              environments like [hyper-v], [local-hyper-v], [machine] and
            //                              [xenserver].  This is required for those environments.

            ""PremiseSubnet"": ""10.0.0.0/24"",
            
            //  NodesSubnet                 Specifies where the neonCLUSTER Docker host node IP addresses
            //                              will be located.  This may be any valid subnet for on-premise 
            //                              deployments but will typically a <b>/24</b> or larger.
            //                              This is determined automatically for cloud environments.
            
            ""NodesSubnet"": ""10.0.0.0/24""

            //  PublicSubnet                IP subnet assigned to the standard public cluster
            //                              overlay network.  This defaults to [10.249.0.0/16].
            //
            //  PublicAttachable            Allow non-Docker Swarm mode service containers to 
            //                              attach to the standard public cluster overlay network.
            //                              This defaults to [true] for flexibility but you may 
            //                              consider disabling this for better security.
            //
            //  PrivateSubnet               IP subnet assigned to the standard private cluster
            //                              overlay network.  This defaults to [10.248.0.0/16].
            //
            //  PrivateAttachable           Allow non-Docker Swarm mode service containers to 
            //                              attach to the standard private cluster overlay network.
            //                              This defaults to [true] for flexibility but you may 
            //                              consider disabling this for better security.
            //
            //  Nameservers                 The IP addresses of the upstream DNS nameservers to be 
            //                              used by the cluster.  This defaults to the Google Public
            //                              DNS servers: [ ""8.8.8.8"", ""8.8.4.4"" ] when the
            //                              property is NULL or empty.
            //
            //  IMPORTANT: [PdnsServerPackageUri] and [PdnsBackendRemotePackageUri] must reference
            //             packages from the same PowerDNS build.
            //
            //  PdnsServerPackageUri         URI for the PowerDNS Authoritative Server package to
            //                              be installed on manager nodes.  This defaults to
            //                              a known good release.
            //
            //  PdnsBackendRemotePackageUri URI for the PowerDNS Authoritative Server remote
            //                              backend package to be installed on manager nodes.  
            //                              This defaults to a known good release.
            //
            //
            //  PdnsRecursorPackageUri      URI for the PowerDNS Recursor package to be installed 
            //                              on all cluster nodes.  This defaults to a known good 
            //                              release.
            //
            //  DynamicDns                  Enables the dynamic DNS services.  This defaults to [true].
        },

    // Options describing the default overlay network created for the 

    // HashiCorp Consul distributed service discovery and key/valuestore settings.
    // Note that Consul is available in every cluster.
    
    ""Consul"": {

        //  Version               The version to be installed.  This defaults to
        //                        a reasonable recent version.
        //
        //  EncryptionKey         16-byte shared secret (Base64) used to encrypt 
        //                        Consul network traffic.  This defaults to
        //                        a cryptographically generated key.  Use the 
        //                        command below to generate a custom key:
        //
        //                              neon create cyhper 
    },

    // HashiCorp Vault secret server options.
    //
    // Note: Vault depends on Consul which must be enabled.

    ""Vault"": {

        //  Version               The version to be installed.  This defaults to
        //                        a reasonable recent version.
        //
        //  KeyCount              The number of unseal keys to be generated by 
        //                        Vault when it is initialized.  This defaults to [1].
        //
        //  KeyThreshold          The minimum number of unseal keys that will be 
        //                        required to unseal Vault.  This defaults to [1].
        //
        //  MaximumLease          The maximum allowed TTL for a Vault token or secret.  
        //                        This limit will be silently enforced by Vault.  This 
        //                        can be expressed as hours with an [h] suffix, minutes 
        //                        using [m] and seconds using [s].  You can also combine
        //                        these like [10h30m10s].  This defaults to [0] which
        //                        specifies about 290 years (essentially infinity).
        //
        //  DefaultLease          The default allowed TTL for a new Vault token or secret 
        //                        if no other duration is specified .  This can be expressed
        //                        as hours with an [h] suffix, minutes using [m] and seconds
        //                        using [s].  You can also combine these like [10h30m10s].  
        //                        This defaults to [0] which specifies about 290 years 
        //                        (essentially infinity).
        //
        //  AutoUnseal            Specifies whether Vault instances should be automatically
        //                        unsealed after restart (at the cost of somewhat lower
        //                        security.  This defaults to [true].

        ""KeyCount"": 1,
        ""KeyThreshold"": 1,
        ""MaximumLease"": ""0"",
        ""DefaultLease"": ""0"",
        ""AutoUnseal"": true
    },

    // Cluster logging options.

    ""Log"": {

        //  Enabled               Indicates that the cluster logging pipeline will be enabled.
        //                        This defaults to [true].
        //
        //  EsImage               The [Elasticsearch] Docker image to be used
        //                        to persist cluster log events.  This defaults to 
        //                        [neoncluster/elasticsearch:latest].
        //
        //  EsShards              The number of Elasticsearch shards. This defaults to 1.
        //
        //  EsReplicas            The number of times Elasticsearch will replicate 
        //                        data within the logging cluster for fault tolerance.
        //                        This defaults to 0 which provides the greatest 
        //                        data capacity at the cost of no fault tolerance.
        //
        //  EsMemory              The amount of RAM to dedicate to each cluster log
        //                        related Elasticsearch container.  This can be expressed
        //                        as ### or ###B (bytes), ###K (kilobytes), ###M (megabytes),
        //                        or ###G (gigabytes).  This defaults to 2G.
        //
        //  KibanaImage           The [Kibana] Docker image to be used to present the
        //                        cluster log user interface.  This defaults to
        //                        [neoncluster/kibana:latest].
        //
        //  HostImage             The Docker image to be run as a local container on
        //                        every node to forward host log events to the cluster
        //                        log aggregator.  This defaults to
        //                        [neoncluster/neon-log-host:latest].
        //
        //  CollectorImage        The Docker image to be run as a service on the 
        //                        cluster that aggregates log events from the node
        //                        log forwarders and pushes them into Elasticsearch.
        //                        This defaults to  [neoncluster/neon-log-collector:latest].
        //
        //  CollectorInstances    The number of TD-Agent based collectors to be deployed
        //                        to receive, transform, and persist events collected by
        //                        the cluster nodes.  This defaults to 1.
        //
        //  CollectorConstraints  Zero or more Docker Swarm style container placement
        //                        constraints referencing built-in or custom
        //                        node labels used to locate TD-Agent collector
        //                        containers.
        //
        //  MetricBeatImage       Identifies the [Elastic Metricbeat] container image 
        //                        to be run as a service on every node of the cluster to
        ///                       capture Docker host node metrics.  This defaults to
        //                        [neoncluster/metricbeat:latest].

        // IMPORTANT: At least one node must have [Labels.LogEsData=true]
        //            when logging is enabled.  This specifies where cluster
        //            log data is to be stored.

        // IMPORTANT: The Elasticsearch and Kibana images must deploy compatible
        //            versions of these service.

        ""Enabled"": true
    },

    // Cluster built-in dashboard options:

    ""Dashboard"": {
    
        // Enables the Elastic Kibana dashboard.  Defaults to [true].

        ""Kibana"": true,

        // Enables the Consul dashboard.  Defaults to [true].

        ""Consul"": true,

        // Enables the Ceph dashboard.  Defaults to [true].

        ""Ceph"": true
    },

    // Integrated Ceph Storage Cluster settings.

`   ""Ceph"": {

        // Indicates whether Ceph storage is to be enabled for the cluster.  
        // This defaults to [true].

        ""Enabled"": true,

        // Specifies the default size of the Ceph drive created for cloud and
        // hypervisor based environments.  This can be a long byte count or a long
        // with units like [512MB] or [2GB].  This can be overridden 
        // for specific nodes.  This defaults to [16GB].
        //
        // NOTE: The default is probably too small for production environments.

        ""OSDDriveSize"": ""16GB"", 

        // Specifies the default amount of RAM to allocate to Ceph for caching.
        // This can be a long byte count or a long with units like [512MB]
        // or [2GB].  This can be overridden for specific nodes.  This defaults
        // to [256MB].
        //
        // NOTE: The default is probably too small for production environments.

        ""OSDCacheSize"": ""256MB"",

        // Specifies the disk capacity in megabytes to assign to the Ceph OSD 
        // journal This can be a long byte count or a long with units like [512MB]
        // or [2GB].  This can be overridden for specific nodes.  This defaults
        // to [1GB].
        //
        // NOTE: The default is probably too small for production environments.

        ""OSDJournalSize"": ""1GB"",

        // Specifies the maximum size of a Ceph RADOS object in bytes.  This can be a 
        // long byte count or a long with units like [512MB] or [2GB].  This 
        // is a global cluster setting that defaults to [5GB].

        ""OSDObjectSizeMax"": ""5GB"",

        // Specifies the default number of object replicas to be stored in the cluster.
        // This defaults to the minimum of 3 or the number of OSD nodes provisioned
        // in the cluster.

        ""OSDReplicaCount"": ""3"",

        // Specifies the minimum number of objects replicas required when the
        // Ceph storage cluster is operating in a degraded state.  This defaults
        // to [ReplicaCount-1] unless [ReplicaCount==1] in which case this will 
        // also default to 1.

        ""OSDReplicaCountMin"": ""2"",

        // Specifies the default number of placement groups assigned to each OSD.
        // This defaults to [100].

        ""OSDPlacementGroups"": ""100"",

        // Specifies the default amount of RAM to allocate to Ceph MDS processes for 
        // caching.  This can be a long byte count or a long with units like [512MB] 
        // or [2GB].  This can be overridden for specific nodes.  This defaults
        // to [64MB].
        //
        // NOTE: The Ceph documentation states that MDS may tend to underestimate the 
        //       RAM it's  using by up to 1.5 times.  To avoid potential memory issues, 
        //       neonCLUSTER will adjust this value by dividing it  by 1.5 to when 
        //       actually configuring the  MDS services.
        //
        // NOTE: You should also take care to leave 1-2GB of RAM for the host Linux 
        //       operating system as well as the OSD non-cache related memory when 
        //       you're configuring this property.
        //
        // NOTE: The default is probably too small for production environments

        ""MDSCacheSize"": ""64MB""
    },

    //-------------------------------------------------------------------------
    // This section describes the physical and/or virtual machines that 
    // will host your cluster.  There are three basic types of nodes:
    //
    //      * Managers
    //      * Workers
    //      * Pets
    //
    // Manager and Worker form the Docker Swarm with the manager nodes handling
    // nodes handle the cluster management tasks.  Both Managers and Workers
    // can host Swarm services.  Pet nodes are part of the neonCLUSTER but are
    // not part of the Docker Swarm.
    //
    // Pet nodes are configured with Docker (non-Swarm) and also take advantage 
    // of many neonCLUSTER services such as logging, DNS, the apt-cache, Consul,
    // Vault and the Docker registry cache.  Pet nodes are generally used to
    // host things you really care about on an individual basis like databases 
    // and other services that aren't really appropriate for a Docker Swarm.
    //
    // Node Properties
    // ---------------
    //
    //      Name                The unique node name (case insensitive)
    //
    //      PrivateAddress      Internal cluster IP address of the node
    //
    //      PublicAddress       Optional public IP address or FQDN of the
    //                          node for local deployments.  This will be 
    //                          configured automatically for cloud deployments
    //                          to AWS, Azure, Google,...
    //
    //      Role                Identifies the type of node being deployed,
    //                          one of:
    //
    //                              manager, worker, or pet
    //
    //      HostGroups          Optional array of string identifing the
    //                          non-standard Ansible group memberships for
    //                          the node.  These groups will be available
    //                          in the Ansible host inventory file when
    //                          executing running Ansible playbooks.
    //
    //  The following properties are honored when provisioning nodes
    //  as virtual machines to the the [local-hyper-v] and [xenserver]
    //  hosting environmewnts.  Many of these will override the matching
    //  [Hosting] options, allowing you to override those defaults for
    //  specific nodes.
    //
    //      VmHost              Identifies the hypervisor host machine from
    //                          [Hosting.VmHosts] by name where the node
    //                          is to be provisioned as a virtual machine.
    //
    //      VmProcessors        The number of virtual processors assigned.
    //
    //      VmMemory            Specifies the maximum amount of memory to 
    //                          allocate to this node when provisioned on a 
    //                          hypervisor.   This is specified as a string 
    //                          that can be a long byte count or a long with
    //                          units like [512MB] or [2GB].
    //
    //      VmMinimumMemory     Specifies the minimum amount of memory to 
    //                          allocate to each cluster virtual machine.  
    //                          This is specified as a string that can be 
    //                          a long byte count or a long with units like
    //                          [512MB] or [2GB].
    //
    //      VmDisk              The amount of disk space to allocate to this
    //                          node when when provisioned on a hypervisor.  
    //                          This is specified as a string that can be a
    //                          long byte count or a long with units like 
    //                          [512MB] or [2GB].
    //
    //  
    //
    // Node Labels
    // -----------
    // Node details can be specified using Docker labels.  These labels
    // will be passed to the Docker daemon when it is launched so they
    // will be available for Swarm filtering operations.  Some labels
    // are also used during cluster configuration.
    //
    // You'll use the [Labels] property to specify labels.  neonCLUSTER
    // predefines several labels.  You may extend these using [Labels.Custom].
    //
    // The following reserved labels are currently supported (see the documentation
    // for more details):
    //
    //      StorageCapacityGB             Storage in GB (int)
    //      StorageLocal                  Storage is local (bool)
    //      StorageSSD                    Storage is backed by SSD (bool)
    //      StorageRedundant              Storage is redundant (bool)
    //      StorageEphemeral              Storage is ephemeral (bool)
    //
    //      ComputeCores                  Number of CPU cores (int)
    //      ComputeArchitecture           x32, x64, arm32, arm64
    //      ComputeRamMB                  RAM in MB (int)
    //      ComputeSwap                   Allow swapping of RAM to disk (default=false)
    //
    //      PhysicalLocation              Location (string)
    //      PhysicalMachine               Computer model or VM size (string)
    //      PhysicalFaultDomain           Fault domain (string)
    //      PhysicalPower                 Power details (string)
    //
    //      LogEsData                     Host Elasticsearch node for cluster
    //                                    logging data (bool)
    //
    //      CephMDS                       Deploy Ceph MDS to the node (bool)
    //      CephMON                       Deploy Ceph Monitor the node (bool)
    //      CephOSD                       Deploy Ceph OSD to the node (bool)
    //      CephDriveSizeGB               Ceph OSD drive size in GB (int)
    //      CephCacheSizeMB               Caph OSD cache size in MB (int)
    //
    // IMPORTANT: Be sure to set [StorageSSD=true] if your node is backed 
    //            by a SSD so that cluster setup will tune Linux for better 
    //            performance.
    //
    // NOTE:      Docker does not support whitespace in label values.
    //
    // NOTE:      These labels will be provisioned as Docker node labels
    //            (not engine labels).  The built-in labels can be referenced
    //            in Swarm constraint expressions as:
    //
    //                  node.labels.io.neon.[built-in name (lowercase)]
    //
    //            Custom labels can be referenced via:
    //
    //                  node.labels[custom name (lowercase)]
    //
    //            Note that the prefix The [node.labels.io.neoncluster] prefix
    //            is reserved for neonCLUSTER related labels.

    ""Nodes"": {

        //---------------------------------------------------------------------
        // Define the cluster management nodes by setting [Manager=true].
        // Management nodes host Consul service discovery, Vault secret 
        // management, and the Docker Swarm managers.
        // 
        // neonCLUSTERs must have at least one manager node.  To have
        // high availability, you may deploy three or five management node.
        // Only an odd number of management nodes are allowed up to a
        // maximum of five.  A majority of these must be healthy for the 
        // cluster as a whole to function.

        ""manager-0"": {
            ""PrivateAddress"": ""10.0.0.30"",
            ""Role"": ""manager"",
            ""Labels"": {
                ""LogEsData"": true,
                ""StorageSSD"": true,
                ""Custom"": {
                    ""mylabel"": ""Hello-World!""
                }
            }
        },
        ""manager-1"": {
            ""PrivateAddress"": ""10.0.0.31"",
            ""Role"": ""manager"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""manager-2"": {
            ""PrivateAddress"": ""10.0.0.32"",
            ""Role"": ""manager"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },

        //---------------------------------------------------------------------
        // Define the worker cluster nodes by setting [Role=worker] (which
        // is the default).  Worker nodes are provisioned in the Docker Swarm.

        ""worker-0"": {
            ""Role"": ""worker"",
            ""PrivateAddress"": ""10.0.0.40"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""worker-1"": {
            ""Role"": ""worker"",
            ""PrivateAddress"": ""10.0.0.41"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""worker-2"": {
            ""Role"": ""worker"",
            ""PrivateAddress"": ""10.0.0.42"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""worker-3"": {
            ""Role"": ""worker"",
            ""PrivateAddress"": ""10.0.0.43"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },

        //---------------------------------------------------------------------
        // Define the pet cluster nodes by setting [Role=pet].  Individual nodes 
        // are part of the neonCLUSTER but not the Docker Swarm.

        ""pet-0"": {
            ""Role"": ""pet"",
            ""PrivateAddress"": ""10.0.0.44"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        }
    }
}

//-----------------------------------------------------------------------------
// Cluster definition files are preprocessed to remove comments as well as to
// implement variables and conditionals:
//
//      * Comment lines
//
//      * Variables defined like: 
//
//          #define myvar1
//          #define myvar2=Hello
//
//      * Variables referenced via:
//
//          $<myvar1>
//
//      * Environment variables referenced via:
//
//          $<<ENV_VAR>>
//
//      * If statements:
//
//          #define DEBUG=TRUE
//          #if $<DEBUG>==TRUE
//              Do something
//          #else
//              Do something else
//          #endif
//
//          #if defined(DEBUG)
//              Do something
//          #endif
//
//          #if undefined(DEBUG)
//              Do something
//          #endif
//
//      * Switch statements:
//
//          #define datacenter=uswest
//          #switch $<datacenter>
//              #case uswest
//                  Do something
//              #case useast
//                  Do something else
//              #default
//                  Do the default thing
//          #endswitch
";
            // We need to output the string line-by-line because Docker appears to
            // add an extra CR per line when streaming output from the [neon-cli] 
            // container out to the workstation.

            using (var reader = new StringReader(sampleJson))
            {
                foreach (var line in reader.Lines())
                {
                    Console.WriteLine(line);
                }
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: true);
        }
    }
}
