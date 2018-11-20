//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.cs
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
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

using NeonCli.Ansible;

namespace NeonCli
{
    public partial class AnsibleCommand : CommandBase
    {
        /// <summary>
        /// Executes a built-in neonHIVE Ansible module. 
        /// </summary>
        /// <param name="login">The hive login.</param>
        /// <param name="commandLine">The module command line: MODULE ARGS...</param>
        private void ExecuteModule(HiveLogin login, CommandLine commandLine)
        {
            var module = commandLine.Arguments.ElementAtOrDefault(0);

            if (commandLine.HasHelpOption || module == null)
            {
                Console.WriteLine(moduleHelp);
                Program.Exit(0);
            }

            var context = new ModuleContext()
            {
                Module = module
            };

            try
            {
                // Verify that we're running in the context of another Ansible
                // command (probably [exec] or [play]).

                if (Environment.GetEnvironmentVariable("IN_NEON_ANSIBLE_COMMAND") == null)
                {
                    throw new NotSupportedException("Built-in neonHIVE Ansible modules can run only within [neon ansible exec] or [play].");
                }

                // Read the Ansible module arguments.

                var argsPath = commandLine.Arguments.ElementAtOrDefault(1);

                if (string.IsNullOrEmpty(argsPath))
                {
                    throw new ArgumentException("Expected a path to the module arguments file.");
                }

                context.Login = login;

                context.SetArguments(argsPath);

                // Connect to the hive so the [HiverHelper] methods will work.

                HiveHelper.OpenHive(login);

                // Run the module.

                switch (module.ToLowerInvariant())
                {
                    case "neon_certificate":

                        new CertificateModule().Run(context);
                        break;

                    case "neon_couchbase_import":

                        new CouchbaseImportModule().Run(context);
                        break;

                    case "neon_couchbase_index":

                        new CouchbaseIndexModule().Run(context);
                        break;

                    case "neon_couchbase_query":

                        new CouchbaseQueryModule().Run(context);
                        break;

                    case "neon_dashboard":

                        new DashboardModule().Run(context);
                        break;

                    case "neon_docker_config":

                        new DockerConfigModule().Run(context);
                        break;

                    case "neon_docker_login":

                        new DockerLoginModule().Run(context);
                        break;

                    case "neon_docker_registry":

                        new DockerRegistryModule().Run(context);
                        break;

                    case "neon_docker_secret":

                        new DockerSecretModule().Run(context);
                        break;

                    case "neon_docker_service":

                        new DockerServiceModule().Run(context);
                        break;

                    case "neon_docker_stack":

                        new DockerStackModule().Run(context);
                        break;

                    case "neon_globals":

                        new GlobalsModule().Run(context);
                        break;

                    case "neon_hive_dns":

                        new HiveDnsModule().Run(context);
                        break;

                    case "neon_hivemq":

                        new HiveMQModule().Run(context);
                        break;

                    case "neon_traffic_manager":

                        new TrafficManagerModule().Run(context);
                        break;

                    default:

                        throw new ArgumentException($"[{module}] is not a recognized neonHIVE Ansible module.");
                }
            }
            catch (Exception e)
            {
                context.Failed  = true;
                context.Message = NeonHelper.ExceptionError(e);

                context.WriteErrorLine(context.Message);
                context.WriteErrorLine(e.StackTrace.ToString());
            }

            // Handle non-exception based errors.

            if (context.HasErrors && !context.Failed)
            {
                context.Failed  = true;
                context.Message = context.GetFirstError();
            }

            Console.WriteLine(context.ToString());

            // Exit right now to be sure that nothing else is written to STDOUT.

            Program.Exit(0);
        }
    }
}
