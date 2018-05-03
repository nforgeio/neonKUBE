//-----------------------------------------------------------------------------
// FILE:	    ServiceContainerSpec.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Service container/task specification.
    /// </summary>
    public class ServiceContainerSpec : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceContainerSpec()
        {
        }

        /// <summary>
        /// The image to use for the container.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// The container labels formatted as <b>LABEL=VALUE</b>.
        /// </summary>
        public List<string> Labels { get; set; }

        /// <summary>
        /// The command to be run in the image.
        /// </summary>
        public List<string> Command { get; set; }

        /// <summary>
        /// Arguments to the command.
        /// </summary>
        public List<string> Args { get; set; }

        /// <summary>
        /// Hostname for the container.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Environment variables of the form <b>VARIABLE=VALUE</b> or <b>VARIABLE</b>
        /// to pass environment variables from the Docker host.
        /// </summary>
        public List<string> Env { get; set; }

        /// <summary>
        /// The container working directory where commands will run.
        /// </summary>
        public string Dir { get; set; }

        /// <summary>
        /// The user within the container.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// The list of additional groups that the command will run as.
        /// </summary>
        public List<string> Groups { get; set; }

        /// <summary>
        /// Security options for the container.
        /// </summary>
        public ServicePrivileges Privileges { get; set;}

        /// <summary>
        /// Optionally create a pseudo TTY.
        /// </summary>
        public bool TTY { get; set; }

        /// <summary>
        /// Open STDIN.
        /// </summary>
        public bool OpenStdIn { get; set; }

        /// <summary>
        /// Specifies file system mounts to be added to the service containers.
        /// </summary>
        public List<ServiceMount> Mounts { get; set; }

        /// <summary>
        /// Signal to be used to gracefully stop the service containers.
        /// </summary>
        public string StopSignal { get; set; }

        /// <summary>
        /// DNS resolver configuration.
        /// </summary>
        public ServiceDnsConfig DnsConfig { get; set; }

        /// <summary>
        /// Specifies the secrets to be exposed to the service containers.
        /// </summary>
        public List<ServiceSecret> Secrets { get; set; }

        /// <summary>
        /// Specifies the configs to be exposed to the service containers.
        /// </summary>
        public List<ServiceConfig> Configs { get; set; }

        /// <summary>
        /// <b>Windows Only:</b> Specifies the isolation technology to be used
        /// for the service containers.
        /// </summary>
        public ServiceIsolationMode Isolation { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Labels  = Labels ?? new List<string>();
            Command = Command ?? new List<string>();
            Args    = Args ?? new List<string>();
            Groups  = Groups ?? new List<string>();
            Secrets = Secrets ?? new List<ServiceSecret>();
            Configs = Configs ?? new List<ServiceConfig>();

            Privileges?.Normalize();
            DnsConfig?.Normalize();
        }
    }
}
