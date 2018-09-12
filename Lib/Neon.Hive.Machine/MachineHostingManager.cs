//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using System.Text.RegularExpressions;
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

namespace Neon.Hive
{
    /// <summary>
    /// Manages hive provisioning directly on bare metal or virtual machines.
    /// </summary>
    [HostingProvider(HostingEnvironments.Machine)]
    public partial class MachineHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this.
        }

        //---------------------------------------------------------------------
        // Instance members

        private HiveProxy                       hive;
        private SetupController<NodeDefinition> controller;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hive">The hive being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public MachineHostingManager(HiveProxy hive, string logFolder = null)
        {
            hive.HostingManager = this;

            this.hive = hive;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public override void Validate(HiveDefinition hiveDefinition)
        {
            // Ensure that the OSD Bluestore block device have been specified.

            if (hive.Definition.HiveFS.Enabled)
            {
                foreach (var node in hive.Definition.Nodes.Where(n => n.Labels.CephOSD))
                {
                    if (string.IsNullOrEmpty(node.Labels.CephOSDDevice))
                    {
                        throw new HiveDefinitionException($"Hosting environment [{HostingEnvironments.Machine}] requires that the OSD node [{node.Name}] explicitly set [{nameof(NodeLabels)}.{nameof(NodeLabels.CephOSDDevice)}] to the target OSD block device (like: [/dev/sdb]).");
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // Perform the provisioning operations.

            controller = new SetupController<NodeDefinition>($"Provisioning [{hive.Definition.Name}] hive", hive.Nodes)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = this.MaxParallel
            };

            controller.AddStep("node labels", (node, stepDelay) => SetLabels(node));

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: hive.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override void AddPostVpnSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override List<HostedEndpoint> GetPublicEndpoints()
        {
            // Note that public endpoints have to be managed manually for
            // on-premise hive deployments so we're going to return an 
            // empty list.

            return new List<HostedEndpoint>();
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => false;

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
            // Note that public endpoints have to be managed manually for
            // on-premise hive deployments.
        }

        /// <inheritdoc/>
        public override string DrivePrefix
        {
            get { return "sd"; }
        }

        /// <summary>
        /// Inspects the node to determine physical machine capabilities like
        /// processor count, RAM, and primary disk capacity and then sets the
        /// corresponding node labels.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void SetLabels(SshProxy<NodeDefinition> node)
        {
            CommandResponse result;

            // Download [/proc/meminfo] and extract the [MemTotal] value (in kB).

            result = node.SudoCommand("cat /proc/meminfo");

            if (result.ExitCode == 0)
            {
                var memInfo       = result.OutputText;
                var memTotalRegex = new Regex(@"^MemTotal:\s*(?<size>\d+)\s*kB", RegexOptions.Multiline);
                var memMatch      = memTotalRegex.Match(memInfo);

                if (memMatch.Success && long.TryParse(memMatch.Groups["size"].Value, out var memSizeKB))
                {
                    // Note that the RAM reported by Linux is somewhat less than the
                    // physical RAM installed.

                    node.Metadata.Labels.ComputeRamMB = (int)(memSizeKB / 1024);    // Convert KB --> MB
                }
            }

            // Download [/proc/cpuinfo] and count the number of processors.

            result = node.SudoCommand("cat /proc/cpuinfo");

            if (result.ExitCode == 0)
            {
                var cpuInfo          = result.OutputText;
                var processorRegex   = new Regex(@"^processor\s*:\s*\d+", RegexOptions.Multiline);
                var processorMatches = processorRegex.Matches(cpuInfo);

                node.Metadata.Labels.ComputeCores = processorMatches.Count;
            }

            // Determine the primary disk size.

            // $hack(jeff.lill):
            //
            // I'm not entirely sure how to determine which block device is hosting
            // the primary file system for all systems.  For now, I'm just going to
            // assume that this can be one of:
            //
            //      /dev/sda1
            //      /dev/sda
            //      /dev/xvda1
            //      /dev/xvda
            //
            // I'll try each of these in order and setting the label for the
            // first reasonable result we get back.

            var blockDevices = new string[]
                {
                    "/dev/sda1",
                    "/dev/sda",
                    "/dev/xvda1",
                    "/dev/xvda"
                };

            foreach (var blockDevice in blockDevices)
            {
                result = node.SudoCommand($"lsblk -b --output SIZE -n -d {blockDevice}", RunOptions.LogOutput);

                if (result.ExitCode == 0)
                {
                    if (long.TryParse(result.OutputText.Trim(), out var deviceSize) && deviceSize > 0)
                    {
                        node.Metadata.Labels.StorageCapacityGB = (int)(deviceSize / NeonHelper.Giga);
                        break;
                    }
                }
            }
        }
    }
}
