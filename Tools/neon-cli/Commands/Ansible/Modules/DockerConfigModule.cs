//-----------------------------------------------------------------------------
// FILE:	    DockerConfigModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_docker_config</b> Ansible module.
    /// </summary>
    public class DockerConfigModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_docker_config:
        //
        // Synopsis:
        // ---------
        //
        // Manages Docker configs.
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
        // parameter    required    default     choices     comments
        // --------------------------------------------------------------------
        //
        // state        no          present     present     indicates whether the config 
        //                                      absent      should be created or removed
        //
        // name         yes                                 the config name
        //
        // bytes        see comment                         base-64 encoded binary config
        //                                                  data.  One of [bytes] or [text]
        //                                                  must be present if state=present
        //
        // text         see comment                         config text.  One of [bytes] or [text]
        //                                                  must be present if state=present
        //
        // Check Mode:
        // -----------
        //
        // This module supports the [--check] Ansible command line option and [check_mode] task
        // property by determining whether any changes would have been made and also logging
        // a desciption of the changes when Ansible verbosity is increased.
        //
        // Remarks:
        // --------
        //
        // This module simply adds or removes a named Docker config.  Note that you cannot
        // add or remove configs if they are already referenced by a Docker service.
        //
        // IMPORTANT: It not currently possible to update an existing config.
        //
        // Examples:
        // ---------
        //
        // This example adds a textual config:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: create config
        //        neon_docker_config:
        //          name: my-config
        //          state: present
        //          text: hello
        //
        // This example adds a binary config:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: create config
        //        neon_docker_config:
        //          name: my-config
        //          state: present
        //          bytes: cGFzc3dvcmQ=
        //
        // This example removes a config:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: remove config
        //        neon_docker_config:
        //          name: my-config
        //          state: absent

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var configName))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [text]");

            context.Arguments.TryGetValue<string>("text", out var configText);
            context.Arguments.TryGetValue<string>("bytes", out var configBytes);

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.

            context.WriteLine(AnsibleVerbosity.Trace, $"Inspecting [{configName}] config.");

            var manager     = cluster.GetHealthyManager();
            var response    = manager.DockerCommand(RunOptions.None, "docker config inspect", configName);
            var configState = (string)null;
            var bytes       = (byte[])null;

            if (response.ExitCode == 0)
            {
                configState = response.OutputText;
                context.WriteLine(AnsibleVerbosity.Trace, $"{configName}] config exists.");
            }
            else
            {
                // $todo(jeff.lill): 
                //
                // I'm trying to distinguish between a a failure because the config doesn't
                // exist and other potential failures (e.g. Docker is not running).
                //
                // This is a bit fragile.

                if (response.ErrorText.StartsWith("Status: Error: no such config:", StringComparison.InvariantCultureIgnoreCase))
                {
                    context.WriteLine(AnsibleVerbosity.Trace, $"[{configName}] config does not exist.");
                }
                else
                {
                    context.WriteErrorLine(response.ErrorText);
                    return;
                }
            }

            switch (state)
            {
                case "absent":

                    if (configState != null)
                    {
                        context.Changed = !context.CheckMode;

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Config [{configName}] will be removed when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing config [{configName}].");

                            response = manager.DockerCommand("docker config rm", configName);

                            if (response.ExitCode == 0)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"[{configName}] config was removed.");
                            }
                            else
                            {
                                context.WriteErrorLine(response.AllText);
                            }
                        }
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"Condig [{configName}] does not exist.");
                    }
                    break;

                case "present":

                    if (configText == null && configBytes == null)
                    {
                        context.WriteErrorLine("One of the [text] or [bytes] module parameters is required.");
                        return;
                    }
                    else if (configText != null && configBytes != null)
                    {
                        context.WriteErrorLine("Only one of [text] or [bytes] can be specified.");
                        return;
                    }

                    if (configBytes != null)
                    {
                        try
                        {
                            bytes = Convert.FromBase64String(configBytes);
                        }
                        catch
                        {
                            context.WriteErrorLine("Only one of [bytes] is not a valid base-64 encoded value.");
                            return;
                        }
                    }

                    if (configState != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"Config [{configName}] already exists.");
                    }
                    else
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Config [{configName}] will be created when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Creating config [{configName}].");

                            var bundle = new CommandBundle("docker", "config", "create", configName, "config-file");

                            if (configBytes != null)
                            {
                                bundle.AddFile("config-file", bytes);
                            }
                            else
                            {
                                bundle.AddFile("config-file", configText);
                            }

                            response = manager.SudoCommand(bundle, RunOptions.Redact);

                            if (response.ExitCode == 0)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"[{configName}] config was created.");
                                context.Changed = !context.CheckMode;
                            }
                            else
                            {
                                context.WriteErrorLine(response.AllText);
                            }
                        }
                    }
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
