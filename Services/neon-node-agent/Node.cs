//-----------------------------------------------------------------------------
// FILE:	    Node.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Retry;
using Neon.Kube.Operator;

using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Prometheus;
using Tomlyn;

namespace NeonNodeAgent
{
    /// <summary>
    /// Abstracts access to the host node.
    /// </summary>
    public static class Node
    {
        /// <summary>
        /// The Linux path where the host node's file system is mounted into the container.
        /// </summary>
        public const string HostMount = "/mnt/host";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Node()
        {
            try
            {
                Name = File.ReadAllLines(LinuxPath.Combine(HostMount, "etc/hostname")).First().Trim();
            }
            catch
            {
                Name = "UNKNOWN";
            }

            AgentId = Guid.NewGuid().ToString("d");
        }

        /// <summary>
        /// Returns the cluster node's host name.
        /// </summary>
        public static string Name { get; private set; }

        /// <summary>
        /// Returns a globally unique ID for the executing node agent.
        /// </summary>
        public static string AgentId { get; private set; }

        /// <summary>
        /// <para>
        /// Executes a command on the host node, setting the file system root to <see cref="HostMount"/>
        /// where the host's file system is mounted.
        /// </para>
        /// <note>
        /// WARNING! This relies on the pod's environment variables like PATH matching the host
        /// environment, which is currently the case because the Microsoft .NET container images
        /// are based on Ubuntu, as are neonKUBE cluster nodes.
        /// </note>
        /// </summary>
        /// <param name="command">The fully qualified path to the command to be executed (relative to the host file system).</param>
        /// <param name="args">Optional command arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static ExecuteResponse ExecuteCapture(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            var actualArgs = new List<object>();

            actualArgs.Add(HostMount);
            actualArgs.Add(command);

            foreach (var arg in args)
            {
                actualArgs.Add(arg);
            }

            return NeonHelper.ExecuteCapture("chroot", actualArgs.ToArray());
        }
    }
}
