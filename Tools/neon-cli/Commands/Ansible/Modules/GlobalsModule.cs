//-----------------------------------------------------------------------------
// FILE:	    GlobalsModule.cs
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

using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_globals</b> Ansible module.
    /// </summary>
    public class GlobalsModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_globals:
        //
        // Synopsis:
        // ---------
        //
        // Manages global neonHIVE settings.
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
        // state        no          get         get         indicates whether to set or retrieve
        //                                      set         a global setting
        //
        // name         yes                                 the setting name (see remarks)
        //
        // value        see comment                         the value being set, required for [state=set]
        //
        // validate     no          yes         yes/no      verify that the [name] and [value]
        //                                                  are valid when [state=set]
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
        // By default, only the settings considered to be user modifiable may be changed using
        // this module.  You can override this by passing [validate: no].
        //
        // Here are the user modifiable hive settings:
        //
        //      allow-unit-testing  - indicates whether HiveFixture based unit tests are
        //                            allowed for the hive.  (yes/no/true/false/on/off/1/0)
        //
        //      disable-auto-unseal - controls whether [neon-hive-manager] will automatically
        //                            unseal the hive Vault.
        //
        //      log-retention-days  - specifies the number of days of hive logs to be 
        //                            maintained in the Elasticsearch cluster.  This must
        //                            be a positive integer.
        //
        // Examples:
        // ---------
        //
        // This example enables hive unit testing:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: allow unit testing
        //        neon_globals:
        //          state: set
        //          name: allow-unit-testing
        //          value: yes
        //
        // This example has the hive retain 30 days of logs:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: configure logs
        //        neon_globals:
        //          state: set
        //          name: log-retention-days
        //          value: 30
        //
        // This example returns the current number of log retention days:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: configure logs
        //        neon_globals:
        //          state: get
        //          name: log-retention-days
        //
        // This example disables validation to change a non-user modifiable
        // global.  THIS MAY BREAK THINGS: so be very sure you know what
        // you're doing:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: change hive UUID
        //        neon_globals:
        //          state: set
        //          name: uuid
        //          value: 0123456789
        //          validate: no

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "state",
            "name",
            "value",
            "validate"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive   = HiveHelper.Hive;
            var consul = HiveHelper.Consul;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "get";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [value]");

            if (!context.Arguments.TryGetValue<string>("value", out var value) && state == "set")
            {
                throw new ArgumentException($"[value] module argument is required when [state={state}].");
            }

            var validate = context.ParseBool("validate");

            validate = validate ?? true;

            if (context.HasErrors)
            {
                return;
            }
            
            // We have the required arguments, so perform the operation.

            switch (state)
            {
                case "get":

                    if (hive.Globals.TryGetString(name, out var output))
                    {
                        context.WriteLine(AnsibleVerbosity.Important, output);
                    }
                    else
                    {
                        context.WriteErrorLine($"Hive global [{name}] does not exist.");
                    }
                    break;

                case "set":

                    if (validate.Value)
                    {
                        hive.Globals.SetUser(name, value);
                    }
                    else
                    {
                        hive.Globals.Set(name, value);
                    }
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [get] or [set].");
            }
        }
    }
}
