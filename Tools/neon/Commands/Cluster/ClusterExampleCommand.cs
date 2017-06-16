//-----------------------------------------------------------------------------
// FILE:	    ClusterExampleCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

namespace NeonTool
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
// The JSON below defines a Docker cluster with three manager and ten worker
// host nodes.
//
// Naming: Cluster, datacenter and node names may be one or more characters 
//         including case insensitive letters, numbers, dashes, underscores,
//         or periods.

{
    //  Name              Cluster name
    //  Datacenter        Datacenter name
    //  Environment       Describes the type of environment, one of:
    //
    //                      other, dev, test, stage, or prod
    //
    //  TimeSources       The FQDNs or IP addresses of the NTP time sources
    //                    used to synchronize the cluster as an array of
    //                    strings.  Reasonable defaults will be used if
    //                    not specified.
    //
    //  PackageProxy      Optionally specifies the HTTP URL including the port 
    //                    (generally [3142]) of the local cluster server used for 
    //                    proxying and caching access to Ubuntu and Debian APT packages.
    //
    //                    This defaults to [false].

    ""Name"": ""my-cluster"",
    ""Datacenter"": ""Seattle"",
    ""Environment"": ""Development"",
    ""TimeSources"": [ ""0.pool.ntp.org"", ""1.pool.ntp.org"", ""2.pool.ntp.org"" ],
    ""PackageProxy"": null,

    // Cluster hosting provider options:

    ""Hosting"": {

        // Identifies the hosting provider.  The possible values are [aws],
        // [azure], [google], or [bare].  This defaults to [machine].
        //
        // You may need to uncomment and initialize the corresponding section
        // below.  See the documentation for the details.

        ""Provider"": ""machine"",

        // This property is required for on-premise clusters that enable
        // the VPN and should be set to the public IP address or FQDN of
        // the cluster router.

        //""ManagerRouter"": ""myrouter.mydomain.com"",

        // Hosting environment specific options.

        // ""Aws"": { ... }
        // ""Azure"": { ... }
        // ""Google"": { ... }
        // ""Machine"": { ... }
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

    // Cluster host authention options:

    ""HostAuth"": {

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
        // You can also specify the HTTP/HTTPS URI to the binary package to be installed.
        // This is useful for installing a custom build or a development snapshot copied 
        // from https://master.dockerproject.org/.  Be sure to copy the TAR file from:
        //
        //      linux/amd64/docker-<docker-version>-dev.tgz
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
        // that deployed the [test] or [experimental]> build.

        ""Version"": ""latest"",

        // Optionally specifies the URL of the Docker registry the cluster will use to
        // download Docker images.  This defaults to the Public Docker registry: 
        // [https://registry-1.docker.io].
        
        ""Registry"": ""https://registry-1.docker.io"",

        // Optionally specifies the user name used to authenticate with the registry
        // mirror and caches.
        //
        ""RegistryUsername"": """",

        // Optionally specifies the password used to authenticate with the registry
        // mirror and caches.
        // 
        // ""RegistryPassword"": """",
        
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

    ""network"": {
        
        //  PublicSubnet          IP subnet assigned to the standard public cluster
        //                        overlay network.  This defaults to [10.249.0.0/16].
        //
        //  PublicAttachable      Allow non-Docker swarm mode service containers to 
        //                        attach to the standard public cluster overlay network.
        //                        This defaults to [true] for flexibility but you may 
        //                        consider disabling this for better security.
        //
        //  PrivateSubnet         IP subnet assigned to the standard private cluster
        //                        overlay network.  This defaults to [10.248.0.0/16].
        //
        //  PrivateAttachable     Allow non-Docker swarm mode service containers to 
        //                        attach to the standard private cluster overlay network.
        //                        This defaults to [true] for flexibility but you may 
        //                        consider disabling this for better security.
        //
        //  Nameservers           The IP addresses of the upstream DNS nameservers to be 
        //                        used by the cluster.  This defaults to the Google Public
        //                        DNS servers: [ ""8.8.8.8"", ""8.8.4.4"" ] when the
        //                        property is NULL or empty.
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
        //                              neon create key 
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
        // AutoUnseal             Specifies whether Vault instances should be automatically
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
        //  EsReplication         The number of times Elasticsearch will replicate 
        //                        data within the logging cluster for fault tolerance.
        //                        This defaults to 1 which ensures that the greatest 
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

    // Dashboard options:

    ""Dashboard"": {
    
        // Install the Elastic Kibana dashboard if cluster logging is enabled.
        // This defaults to [true].

        ""Kibana"": true,

        // Install the Consul user interface.  This defaults to [true].

        ""Consul"": true
    },

    //-------------------------------------------------------------------------
    // This section describes the physical and/or virtual machines that 
    // will host your cluster.  There are two basic types of nodes:
    //
    //      * Manager Nodes
    //      * Worker Nodes
    //
    // Manager nodes handle the cluster management tasks.  Both types of
    // nodes can host application containers.
    //
    // Node Properties
    // ---------------
    //
    //      Name                The unique node name (case insensitive)
    //      PrivateAddress      Internal cluster IP address of the node
    //
    //      PublicAddress       Optional public IP address or FQDN of the
    //                          node for local deployments.  This will be 
    //                          configured automatically for cloud deployments
    //                          to AWS, Azure, Google,...
    //
    //      manager             [true] for manager nodes (default=false)
    //
    // Node Labels
    // -----------
    // Node details can be specified using Docker labels.  These labels
    // will be passed to the Docker daemon when it is launched so they
    // will be available for Swarm filtering operations.  Some labels
    // are also used during cluster configuration.
    //
    // You'll use the [Labels] property to specifiy labels.  NeonCluster
    // predefines several labels.  You may extend these using [Labels.Custom].
    //
    // The following reserved labels are currently supported (see the documentation
    // for more details):
    //
    //      StorageCapacityDB             Storage in GB (int)
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
    //            is reserved for NeonCluster related labels.

    ""Nodes"": {

        //---------------------------------------------------------------------
        // Describe the cluster management nodes by setting [Manager=true].
        // Management nodes host Consul service discovery, Vault secret 
        // management, and the Docker Swarm managers.
        // 
        // NeonClusters must have at least one manager node.  To have
        // high availability, you may deploy three or five management node.
        // Only an odd number of management nodes are allowed up to a
        // maximum of five.  A majority of these must be healthy for the 
        // cluster as a whole to function.

        ""manage-0"": {
            ""PrivateAddress"": ""10.0.1.30"",
            ""Role"": ""manager"",
            ""Labels"": {
                ""LogEsData"": true,
                ""StorageSSD"": true,
                ""Custom"": {
                    ""mylabel"": ""Hello-World!""
                }
            }
        },
        ""manage-1"": {
            ""PrivateAddress"": ""10.0.1.31"",
            ""Role"": ""manager"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""manage-2"": {
            ""PrivateAddress"": ""10.0.1.30"",
            ""Role"": ""manager"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },

        //---------------------------------------------------------------------
        // Describe the worker cluster nodes by leaving [Manager=false].
        // Swarm will schedule containers to run on these nodes.

        ""node-0"": {
            ""PrivateAddress"": ""10.0.1.40"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""node-1"": {
            ""PrivateAddress"": ""10.0.1.41"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""node-2"": {
            ""PrivateAddress"": ""10.0.1.42"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
        ""node-3"": {
            ""PrivateAddress"": ""10.0.1.43"",
            ""Labels"": {
                ""StorageSSD"": true
            }
        },
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
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true);
        }
    }
}
