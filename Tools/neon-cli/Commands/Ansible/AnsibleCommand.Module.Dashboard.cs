//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Dashboard.cs
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

namespace NeonCli
{
    //---------------------------------------------------------------------
    // neon_dns:
    //
    // Synopsis:
    // ---------
    //
    // Manages neonCLUSTER dashboards.
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
    // title        no                                  title to be used for this dashboard 
    //                                                  when displayed in the global cluster
    //                                                  dashboard
    //
    // folder       no                                  folder where this dashboard will be in 
    //                                                  the global cluster dashboard.
    //
    // url          see comment                         dashboard URL.  Required  
    //                                                  when [state=present]
    //
    // description  no                                  brief dashboard description
    //
    // state        no          present     absent      indicates whether the dashboard
    //                                      present     should be created or removed

    public partial class AnsibleCommand : CommandBase
    {
        /// <summary>
        /// Implements the built-in <b>neon_dashboard</b> module.
        /// </summary>
        /// <param name="context">The module execution context.</param>
        private void RunDashboardModule(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;
            var consul  = NeonClusterHelper.Consul;

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[{name}] is not a valid dashboard name.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

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

            // We have the required arguments, so perform the operation.

            var dashboardKey = $"{NeonClusterConst.ConsulDashboardsKey}/{name}";

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Check if dashboard [{name}] exists.");

                    if (consul.KV.Exists(dashboardKey).Result)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard [{name}] does exist.");

                        if (!context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Deleting dashboard [{name}].");
                            consul.KV.Delete(dashboardKey);
                            context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard [{name}] deleted.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboard [{name}] would be deleted when CHECKMODE is disabled.");
                        }

                        context.Changed = true;
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard [{name}] does not exist.");
                    }
                    break;

                case "present":

                    // Build the dashboard definition from the arguments.

                    var newDashboard = new ClusterDashboard()
                    {
                        Name        = name,
                        Title       = title,
                        Folder      = folder,
                        Url         = url,
                        Description = description
                    };

                    // Validate the dashboard.

                    context.WriteLine(AnsibleVerbosity.Trace, "Validating dashboard.");

                    var errors = newDashboard.Validate(cluster.Definition);

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

                    var existingDashboard = consul.KV.GetObjectOrDefault<ClusterDashboard>(dashboardKey).Result;

                    if (existingDashboard != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard exists: checking for differences.");

                        context.Changed = !NeonHelper.JsonEquals(newDashboard, existingDashboard);

                        if (context.Changed)
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
                        context.Changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"Dashboard for [{name}] does not exist.");
                    }

                    if (context.Changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboard [{name}] would be updated when CHECKMODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating dashboard.");
                            consul.KV.PutObject(dashboardKey, newDashboard).Wait();
                            context.WriteLine(AnsibleVerbosity.Info, $"Dashboard updated.");
                        }
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }
    }
}
