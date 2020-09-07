//-----------------------------------------------------------------------------
// FILE:	    AwsHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

using Amazon;
using Amazon.Runtime;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on Amazon Web Services.
    /// </summary>
    [HostingProvider(HostingEnvironment.Aws)]
    public class AwsHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // IMPLENTATION NOTE:
        //
        // 

        /// <summary>
        /// Describes an Ubuntu ami from the AWS Marketplace.
        /// </summary>
        private class AwsUbuntuImage
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="clusterVersion">Specifies the neonKUBE cluster version.</param>
            /// <param name="ubuntuVersion">Specifies the Ubuntu image version.</param>
            /// <param name="ubuntuBuild">Specifies the Ubuntu build.</param>
            /// <param name="isPrepared">
            /// Pass <c>true</c> for Ubuntu images that have already seen basic
            /// preparation for inclusion into a neonKUBE cluster, or <c>false</c>
            /// for unmodified base Ubuntu images.
            /// <param name="ami">Specifies the AWS VM image ami.</param>
            /// </param>
            public AwsUbuntuImage(string clusterVersion, string ubuntuVersion, string ubuntuBuild, bool isPrepared, string ami)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterVersion), nameof(clusterVersion));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ubuntuVersion), nameof(ubuntuVersion));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ubuntuBuild), nameof(ubuntuBuild));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(ami), nameof(ami));

                this.ClusterVersion = SemanticVersion.Parse(clusterVersion);
                this.UbuntuVersion  = ubuntuVersion;
                this.UbuntuBuild    = ubuntuBuild;
                this.IsPrepared     = isPrepared;
                this.Ami            = ami;
            }

            /// <summary>
            /// Returns the neonKUBE cluster version.
            /// </summary>
            public SemanticVersion ClusterVersion { get; private set; }

            /// <summary>
            /// Returns the Ubuntu version deployed by the image.
            /// </summary>
            public string UbuntuVersion { get; private set; }

            /// <summary>
            /// Returns the Ubuntu build version.
            /// </summary>
            public string UbuntuBuild { get; private set; }

            /// <summary>
            /// Returns <c>true</c> for Ubuntu images that have already seen basic
            /// preparation for inclusion into a neonKUBE cluster.  This will be
            /// <c>false</c> for unmodified base Ubuntu images.
            /// </summary>
            public bool IsPrepared { get; private set; }

            /// <summary>
            /// Returns the AWS image ami.
            /// </summary>
            public string Ami { get; private set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Specifies the ID to use when querying for Canonical images 
        /// </summary>
        private const string canonicalOwnerId = "099720109477";

        /// <summary>
        /// Returns the list of supported Ubuntu images from the AWS Marketplace.
        /// </summary>
        private static IReadOnlyList<AwsUbuntuImage> ubuntuImages;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static AwsHostingManager()
        {
            ubuntuImages = new List<AwsUbuntuImage>()
            {
                new AwsUbuntuImage("0.1.0-alpha", "20.04", "20.04.20200729", isPrepared: false, ami: "ami-056cb9ae6e2df09e8")
            }
            .AsReadOnly();
        }

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        //---------------------------------------------------------------------
        // Instance members

        private KubeSetupInfo           setupInfo;
        private ClusterProxy            cluster;
        private string                  clusterName;
        private HostingOptions          hostingOptions;
        private CloudOptions            cloudOptions;
        private AwsHostingOptions       awsOptions;
        private NetworkOptions          networkOptions;
        private BasicAWSCredentials     awsCredentials;
        private string                  region;
        private string                  resourceGroup;
        private Region                  awsRegion;
        private AmazonEC2Client         ec2;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public AwsHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on AWS.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="setupInfo">Specifies the cluster setup information.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AwsHostingManager(ClusterProxy cluster, KubeSetupInfo setupInfo, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            cluster.HostingManager = this;

            this.setupInfo      = setupInfo;
            this.cluster        = cluster;
            this.clusterName    = cluster.Name;
            this.hostingOptions = cluster.Definition.Hosting;
            this.cloudOptions   = hostingOptions.Cloud;
            this.awsOptions     = hostingOptions.Aws;
            this.networkOptions = cluster.Definition.Network;
            this.region         = awsOptions.Region;
            this.resourceGroup  = awsOptions.ResourceGroup;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }

            ec2?.Dispose();
            ec2 = null;
        }

        /// <summary>
        /// Indicates when an AWS connection is established.
        /// </summary>
        private bool isConnected => ec2 != null;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            AssignNodeAddresses(clusterDefinition);
        }

        /// <inheritdoc/>
        public override async Task<bool> ProvisionAsync(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            var operation  = $"Provisioning [{cluster.Definition.Name}] on AWS [{region}/{resourceGroup}]";
            var controller = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus     = this.ShowStatus,
                ShowNodeStatus = true,
                MaxParallel    = int.MaxValue       // There's no reason to constrain this
            };

            controller.AddGlobalStep("AWS connect", ConnectAwsAsync);

            if (!controller.Run(leaveNodesConnected: false))
            {
                Console.WriteLine("*** One or more AWS provisioning steps failed.");
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        /// <summary>
        /// Establishes the necessary client connections to AWS and validates the credentials,
        /// when a connection has not been established yet.
        /// </summary>
        private async Task ConnectAwsAsync()
        {
            if (isConnected)
            {
                return;
            }

            // Initialize the credentials and clients and retrieve the list of
            // AWS regions to verify that we can connect and perform operations
            // as well as to verify the target region.  We'll then create new
            // client(s) for the target region.

            awsCredentials = new BasicAWSCredentials(awsOptions.AccessKeyId, awsOptions.SecretAccessKey);

            using (var ec2 = new AmazonEC2Client(awsCredentials, RegionEndpoint.USEast1))
            {
                var awsRegions = (await ec2.DescribeRegionsAsync()).Regions;

                awsRegion = awsRegions.SingleOrDefault(r => r.RegionName.Equals(region, StringComparison.InvariantCultureIgnoreCase));

                if (awsRegion == null)
                {
                    throw new KubeException($"AWS region [{region}] does not exist or is not available to your AWS account.");
                }
            }

            ec2 = new AmazonEC2Client(awsCredentials, RegionEndpoint.GetBySystemName(region));
        }

        /// <inheritdoc/>
        public override bool CanManageRouter => true;

        /// <inheritdoc/>
        public override async Task UpdatePublicIngressAsync()
        {
            // $todo(jefflil): Implement this

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task EnablePublicSshAsync()
        {
            // $todo(jefflil): Implement this

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task DisablePublicSshAsync()
        {
            // $todo(jefflil): Implement this

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override string GetDataDisk(SshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            var unpartitonedDisks = node.ListUnpartitionedDisks();

            if (unpartitonedDisks.Count() == 0)
            {
                return "PRIMARY";
            }

            Covenant.Assert(unpartitonedDisks.Count() == 1, "VMs are assumed to have no more than one attached data disk.");

            return unpartitonedDisks.Single();
        }
    }
}
