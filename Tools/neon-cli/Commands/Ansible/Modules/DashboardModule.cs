//-----------------------------------------------------------------------------
// FILE:	    DashboardModule.cs
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

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli.Ansible
{
    //---------------------------------------------------------------------
    // neon_dashboard:
    //
    // Synopsis:
    // ---------
    //
    // Manages neonHIVE dashboards.
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
    // name         yes                                 dashboard name
    //
    // state        no          present     present     create or remove the dashboard
    //                                      absent
    //
    // title        no                                  title to be used for this dashboard 
    //                                                  when displayed in the global hive
    //                                                  dashboard
    //
    // folder       no                                  folder where this dashboard will be in 
    //                                                  the global hive dashboard.
    //
    // url          see comment                         dashboard URL. Required when [state=present]
    //
    // description  no                                  brief dashboard description
    //
    // Examples
    // --------
    //
    // This example creates a dashboard named [google] that displays the
    // search engine:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: google dashboard
    //        neon_dashboard:
    //          name: google
    //          title: Google Search
    //          url: http://www.google.com
    //          description: Everything on the web
    //          state: present
    //
    // This example removes the [google] dashboard if it exists:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: google dashboard
    //        neon_dashboard:
    //          name: google
    //          state: absent

    /// <summary>
    /// Implements the <b>neon_dashboard</b> Ansible module.
    /// </summary>
    public class DashboardModule : IAnsibleModule
    {
        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "name",
            "state",
            "title",
            "folder",
            "url",
            "description",
            "state"
        };

        /// <summary>
        /// Implements the built-in <b>neon_dashboard</b> module.
        /// </summary>
        /// <param name="context">The module context.</param>
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

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!HiveDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[{name}] is not a valid dashboard name.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Check if dashboard [{name}] exists.");

                    if (hive.Dashboard.Get(name) != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard [{name}] already exists.");

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboard [{name}] will be deleted when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Deleting dashboard [{name}].");
                            hive.Dashboard.Remove(name);
                            context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard [{name}] deleted.");
                            context.Changed = true;
                        }
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard [{name}] does not exist.");
                    }
                    break;

                case "present":

                    // Parse the PRESENT arguments.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [url]");

                    if (!context.Arguments.TryGetValue<string>("url", out var url) && state == "present")
                    {
                        throw new ArgumentException($"[url] module argument is required when [state={state}].");
                    }

                    if (!Uri.TryCreate(url, UriKind.Absolute, out var urlParsed))
                    {
                        throw new ArgumentException($"[url={url}] is not valid.");
                    }

                    url = urlParsed.ToString();

                    context.Arguments.TryGetValue<string>("title", out var title);
                    context.Arguments.TryGetValue<string>("folder", out var folder);
                    context.Arguments.TryGetValue<string>("description", out var description);

                    if (context.HasErrors)
                    {
                        return;
                    }

                    // Build the dashboard definition from the arguments.

                    var newDashboard = new HiveDashboard()
                    {
                        Name        = name,
                        Title       = title,
                        Folder      = folder,
                        Url         = url,
                        Description = description
                    };

                    // Validate the dashboard.

                    context.WriteLine(AnsibleVerbosity.Trace, "Validating dashboard.");

                    var errors = newDashboard.Validate(hive.Definition);

                    if (errors.Count > 0)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"[{errors.Count}] dashboard validation errors.");

                        foreach (var error in errors)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, error);
                            context.WriteErrorLine(error);
                        }

                        context.Failed = true;
                        return;
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, "Dashboard is valid.");

                    // Try reading any existing dashboard with this name and then determine
                    // whether the two versions are actually different. 

                    context.WriteLine(AnsibleVerbosity.Trace, $"Looking for existing dashboard [{name}]");

                    var existingDashboard = hive.Dashboard.Get(name);
                    var changed           = false;

                    if (existingDashboard != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard exists: checking for differences.");

                        changed = !NeonHelper.JsonEquals(newDashboard, existingDashboard);

                        if (changed)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Dashboards are different.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboards are the same.  No need to update.");
                        }
                    }
                    else
                    {
                        changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard for [{name}] does not exist.");
                    }

                    if (changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboard [{name}] will be updated when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating dashboard.");
                            hive.Dashboard.Set(newDashboard);
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboard updated.");

                            context.Changed = true;
                        }

                        context.CheckMode = !context.CheckMode;
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
