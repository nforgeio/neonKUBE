//-----------------------------------------------------------------------------
// FILE:	    DockerStackModule.cs
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
    // neon_docker_stack:
    //
    // Synopsis:
    // ---------
    //
    // Manages Docker stacks.
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
    // name                     yes                                 identifies the target stack
    //
    // state                    no          deploy      deploy      deploys a stack
    //                                                  remove      removes a named stack
    //
    // stack                    see comment                         specifies the stack when [state=deploy]
    //
    // The [stack] parameter specifies standard Docker Compose settings that will be
    // deployed to the hive without any changes.  This link describes the format:
    //
    //      https://docs.docker.com/compose/compose-file/#service-configuration-reference
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
    // This example creates a simple stack named [test-vegomatic]:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: stack
    //        neon_docker_stack:
    //          name: test-vegomatic
    //          state: deploy
    //          stack:
    //            version: "3"
    //            services:
    //              vegomatic:
    //                image: nhive/vegomatic
    //                deploy:
    //                  replicas: 1
    //                networks:
    //                  - neon-public
    //
    // This example removes the [test-vegomatic] stack:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: stack
    //        neon_docker_stack:
    //          name: test-vegomatic
    //          state: remove

    /// <summary>
    /// Implements the <b>neon_docker_service</b> Ansible module.
    /// </summary>
    public class DockerStackModule : IAnsibleModule
    {
        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "name",
            "state",
            "stack"
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

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "deploy";
            }

            state = state.ToLowerInvariant();

            var manager  = hive.GetReachableManager();
            var response = (CommandResponse)null;

            switch (state)
            {
                case "deploy":

                    if (!context.Arguments.TryGetValue("stack", out var stackObject))
                    {
                        throw new ArgumentException($"[stack] module argument is required when [state=deploy].");
                    }

                    var stackJson = NeonHelper.JsonSerialize(stackObject);
                    var stackYaml = NeonHelper.JsonToYaml(stackJson);
                    var bundle    = new CommandBundle($"docker stack deploy --compose-file ./compose.yaml {name}");

                    bundle.AddFile("compose.yaml", stackYaml);

                    response = manager.SudoCommand(bundle, RunOptions.None);

                    if (response.ExitCode != 0)
                    {
                        context.WriteErrorLine(response.ErrorText);
                    }

                    context.Changed = true;
                    break;

                case "remove":

                    response = manager.SudoCommand("docker stack rm", RunOptions.None, name);

                    if (response.ExitCode != 0)
                    {
                        context.WriteErrorLine(response.ErrorText);
                    }

                    context.Changed = true;
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [rollback].");
            }
        }
    }
}
