//-----------------------------------------------------------------------------
// FILE:	    QueueModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cryptography;
using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

using NeonCli.Ansible.Docker;

namespace NeonCli.Ansible
{
    //---------------------------------------------------------------------
    // neon_queue:
    //
    // Synopsis:
    // ---------
    //
    // Manages hive message queues.
    //
    // Requirements:
    // -------------
    //
    // This module runs only within the [neon-cli] container when invoked
    // by [neon ansible exec ...] or [neon ansible play ...].
    //
    // Options:
    // --------
    //
    // parameter                required    default     choices     comments
    // --------------------------------------------------------------------
    //
    // state                    no          command     command     executes a command in one of the HiveMQ manager containers.
    //
    // command                  see comment                         array specifying the command and arguments
    //                                                              required if [state=command]
    //
    // This module currently supports only [state=command].  This is typically used to execute a
    // [rabbitmqctl] or [rabbitmqadmin] command within one of the HiveMQ manager containers.  You'll
    // pass the command name and arguments as the [command] parameter.
    //
    // Check Mode:
    // -----------
    //
    // This module does not support the [--check] Ansible command line option and 
    // [check_mode] task property.
    //
    // Examples:
    // ---------
    //
    // This example executes the [rabbitmqctl purge_queue -p app my-queue]
    // command which purges all messages from the [app/my-queue].
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: test
    //        neon_queue:
    //          name: 
    //          state: command
    //          command:
    //            - rabbitmqctl
    //            - purge_queue
    //            - -p
    //            - app
    //            - my-queue

    /// <summary>
    /// Implements the <b>neon_docker_service</b> Ansible module.
    /// </summary>
    public class QueuekModule : IAnsibleModule
    {
        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "state",
            "command"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive = HiveHelper.Hive;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            // Obtain common arguments.

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "command";
            }

            state = state.ToLowerInvariant();

            var manager  = hive.GetReachableManager();
            var response = (CommandResponse)null;

            switch (state)
            {
                case "command":

                    if (!context.Arguments.TryGetValue("command", out var stackObject))
                    {
                        throw new ArgumentException($"[command] module argument is required when [state=command].");
                    }

                    // We need to find a hive node with a running HiveMQ manager container.

                    context.Changed = true;
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [rollback].");
            }
        }
    }
}
