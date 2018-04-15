//-----------------------------------------------------------------------------
// FILE:	    CouchbaseIndexModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Couchbase;
using Couchbase.N1QL;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Net;
using Neon.Retry;

using NeonCli.Ansible.Couchbase;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_couchbase_index</b> Ansible module.
    /// </summary>
    public class CouchbaseIndexModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_couchbase_index:
        //
        // Synopsis:
        // ---------
        //
        // Manages the presence or absense of a Couchbase bucket index. 
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
        // servers      yes                                 array specifying one or more target
        //                                                  Couchbase servers.  Each element can 
        //                                                  be an IP address, a FQDN, cluster
        //                                                  node name or cluster node group name
        //
        // port         no          8091                    Couchbase server port
        //                          18902 (for SSL)
        //
        // ssl          no          no          yes         use SSL to secure the connections
        //                                      no
        //
        // bucket       yes                                 identifies the target bucket
        //
        // username     yes                                 identifies the Couchbase user
        //
        // password     yes                                 specifies the Couchbase password
        //
        // state        no          present     absent      indicates whether the bucket index
        //                                      present     should be created or removed
        //
        // force        no          no          yes         optionally forces an index to be
        //                                                  recreated when [state=present] and
        //                                                  the index already exists
        //                                      no
        //
        // name         yes                                 the index name
        //
        // primary      no          no          yes         indicates that this is PRIMARY index
        //                                      no
        //
        // type         no          gsi         gsi         specifies the index type
        //                                      view
        //
        // keys         see comment                         array of document fields, scalar
        //                                                  functions or array expressions to
        //                                                  be indexed.  This is required for
        //                                                  GSI indexes and must be empty
        //                                                  for primaries.
        //
        // where        no                                  optionally specifies a WHERE clause
        //                                                  to create for a GSI or VIEW index
        //                                                  (see the WARNING in remarks below).
        //
        // nodes        no                                  optional identifies the specific
        //                                                  nodes where the index is to be
        //                                                  hosted.  This is an array of network
        //                                                  endpoints like "node1:8091" or 
        //                                                  '10.0.0.200:8091".  Cannot be used
        //                                                  with [replicas].
        //
        // defer_build  no          no          yes         optionally starts index creation
        //                                      no          but DOES NOT wait for the index
        //                                                  build to complete
        //
        // replicas     no          NODE-COUNT              specifies the number of index
        //                                                  replicas.  This defaults to the
        //                                                  number of nodes in the cluster.
        //                                                  This cannot be used with [nodes].
        //                                      
        // Remarks:
        // --------
        //
        // This module is used to manage Couchbase indexes.  Couchbase supports two basic
        // index types: PRIMARY and GSI (global secondary index).  A PRIMARY index must be
        // created to support N1QL queries and also to support GSI indexes.  PRIMARY indexes
        // may not include [keys] or [where] parameters.
        //
        // GSI indexes must include at least one [keys] element specifying a document
        // property, scalar aggregate function or array expression.  You may also filter
        // the index by specifying a WHERE clause.
        //
        // WARNING!
        //
        // This module is not currently capable of deeply parsing the [where] expression
        // so that it can compared to the canonical form that will be returned as the 
        // [condition] property returned when listing the index.  This is important 
        // because the module uses a simple string comparision to compares the module 
        // [where] parameter above with the index's [condition] property to determine
        // whether the index needs to be rebuilt.
        //
        // This means that you need to ensure that [where] property exactly matches
        // the condition persised with the index.  One way to achieve this is to:
        //
        //      1. Create the index manually using the Couchbase dashboard or
        //         using this Ansible module.
        //
        //      2. Execute the following query in the Couchbase dashboard,
        //         replacing INDEX_NAME with your index's name:
        //
        //         select * from system:indexes where name = INDEX_NAME
        //
        //      3. Look for the [condition] property in the results and
        //         copy it (including the surrounding double quotes) and
        //         then use that as the [where] property.
        //
        // This module will rebuild the index if the [condition] does not EXACTLY
        // match the [where] argument which is probably not what you want.
        //
        // Examples:
        // ---------
        //

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Identifies the index type.
        /// </summary>
        private enum IndexType
        {
            Gsi = 0,
            View
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var cluster    = NeonClusterHelper.Cluster;
            var nodeGroups = cluster.Definition.GetNodeGroups(excludeAllGroup: true);

            //-----------------------------------------------------------------
            // Parse the module arguments.

            var couchbaseArgs = CouchbaseArgs.Parse(context);

            if (couchbaseArgs == null)
            {
                return;
            }

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (!context.Arguments.TryGetValue<bool>("force", out var force))
            {
                force = false;
            }

            var name = context.ParseString("name", v => new Regex(@"[a-z][a-z0-0#_]*", RegexOptions.IgnoreCase).IsMatch(v));

            var primary = context.ParseBool("primary");

            if (!primary.HasValue)
            {
                primary = false;
            }

            var type = context.ParseEnum<IndexType>("type");

            if (!type.HasValue)
            {
                type = default(IndexType);
            }

            var keys  = context.ParseStringArray("keys");
            var where = context.ParseString("where");
            var nodes = context.ParseStringArray("nodes");
            var defer = context.ParseBool("defer_build");

            if (!defer.HasValue)
            {
                defer = false;
            }

            var replicas = context.ParseInt("replicas");

            if (primary.Value)
            {
                if (keys.Count > 0)
                {
                    context.WriteErrorLine("PRIMARY indexes do not allow any [keys] to be specified.");
                    return;
                }

                if (!string.IsNullOrEmpty(where))
                {
                    context.WriteErrorLine("PRIMARY indexes do not support [where] to be specified.");
                    return;
                }
            }
            else
            {
                context.WriteErrorLine("Non-PRIMARY indexes must specify at least one [key].");
                return;
            }

            if (replicas.HasValue && nodes.Count > 0)
            {
                context.WriteErrorLine("Only one of [nodes] or [replicas] may be specified for any index.");
                return;
            }

            //-----------------------------------------------------------------
            // Perform the operation.

            Task.Run(
                async () =>
                {
                    using (var bucket = couchbaseArgs.Settings.OpenBucket(couchbaseArgs.Credentials))
                    {
                        var retry = new LinearRetryPolicy(CouchbaseTransientDetector.IsTransient, sourceModule: bucket.Name);

                        // Fetch the index if it already exists.

                        dynamic existing = await retry.InvokeAsync<dynamic>(
                            async () =>
                            {
                                return (await bucket.QuerySafeAsync<dynamic>($"select * from system:indexes where name = `{name}`")).FirstOrDefault();
                            });

                        switch (state)
                        {
                            case "present":

                                // Default [replicas] to the number of Couchbase nodes if the
                                // count or target nodes aren't explicitly specified.

                                if (!replicas.HasValue && nodes.Count == 0)
                                {
                                    context.WriteLine(AnsibleVerbosity.Trace, "Discovering the number of Couchbase nodes.");

                                    var results = await retry.InvokeAsync(
                                        async () =>
                                        {
                                            return await bucket.QuerySafeAsync<dynamic>("");
                                        });

                                    replicas = results.Count;

                                    context.WriteLine(AnsibleVerbosity.Trace, $"Discovered [{replicas}] Couchbase nodes.");
                                }

                                // Generate the index creation query.

                                var sbCreateIndexQuery = new StringBuilder();

                                if (primary.Value)
                                {
                                    sbCreateIndexQuery.Append($"create primary index {name} on {bucket} using {type.ToString().ToUpperInvariant()}");
                                }
                                else
                                {
                                    sbCreateIndexQuery.Append($"create index {name} on {bucket} using {type.ToString().ToUpperInvariant()}");
                                }

                                // Append the WHERE clause.

                                if (!string.IsNullOrEmpty(where))
                                {
                                    // Ensure that the WHERE clause is surrounded by "( ... )".

                                    if (!where.StartsWith("(") && !where.EndsWith(")"))
                                    {
                                        where = $"({where})";
                                    }

                                    // Now strip the parens off the where clause to be added
                                    // to the query.

                                    var queryWhere = where.Substring(1, where.Length - 2);

                                    // Append the clause.

                                    sbCreateIndexQuery.AppendWithSeparator($"where {queryWhere}");
                                }

                                // Append the WITH clause.

                                sbCreateIndexQuery.AppendWithSeparator("with {");

                                if (defer.Value)
                                {
                                    sbCreateIndexQuery.AppendWithSeparator("\"defer_build\":true");
                                }

                                if (nodes.Count > 0)
                                {
                                    sbCreateIndexQuery.AppendWithSeparator("\"nodes\": [", ", ");

                                    var first = true;

                                    foreach (var node in nodes)
                                    {
                                        if (first)
                                        {
                                            first = false;
                                        }
                                        else
                                        {
                                            sbCreateIndexQuery.Append(", ");
                                        }

                                        sbCreateIndexQuery.Append($"\"{node}\"");
                                    }

                                    sbCreateIndexQuery.Append("]");
                                }
                                else
                                {
                                    sbCreateIndexQuery.AppendWithSeparator($"\"num_replica\":{replicas}", ", ");
                                }

                                sbCreateIndexQuery.AppendWithSeparator("}");

                                // Add or update the index.

                                if (existing == null)
                                {
                                    // An index with this name already exists, so we'll compare its
                                    // properties with the module parameters to determine whether we
                                    // need to remove and recreate it.

                                    var index = existing["indexes"];
                                    var changed = false;

                                    // Compare the old/new index types.

                                    var orgType = (string)index["using"];

                                    if (!string.Equals(orgType, type.ToString(), StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Index type changing from [{orgType}] to [{type}].");
                                    }

                                    // Compare the old/new index keys.

                                    var orgKeys     = (JArray)index["index_key"];
                                    var keysChanged = false;

                                    if (orgKeys.Count != keys.Count)
                                    {
                                        keysChanged = true;
                                    }
                                    else
                                    {
                                        for (int i = 0; i < orgKeys.Count; i++)
                                        {
                                            if ((string)orgKeys[i] != keys[i])
                                            {
                                                keysChanged = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (keysChanged)
                                    {
                                        changed = true;

                                        var sbOrgKeys = new StringBuilder();
                                        var sbNewKeys = new StringBuilder();

                                        foreach (string key in orgKeys)
                                        {
                                            sbOrgKeys.AppendWithSeparator(key, ", ");
                                        }

                                        foreach (string key in keys)
                                        {
                                            sbNewKeys.AppendWithSeparator(key, ", ");
                                        }

                                        context.WriteLine(AnsibleVerbosity.Info, $"Index keys changing from [{sbOrgKeys}] to [{sbNewKeys}].");
                                    }

                                    // Compare the filter condition.

                                    var orgWhere = (string)index["condition"];

                                    if (orgWhere != where)
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Index where clause changing from [{orgWhere ?? string.Empty}] to [{where ?? string.Empty}].");
                                    }

                                    // We need to remove and recreate the index if it differs
                                    // from what was requested.

                                    if (changed)
                                    {
                                        if (context.CheckMode)
                                        {
                                            context.WriteLine(AnsibleVerbosity.Important, $"Index [{name}] will be updated when CHECKMODE is disabled.");
                                        }
                                        else
                                        {
                                            context.Changed = true;

                                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing existing index [{name}].");

                                            await retry.InvokeAsync(
                                                async () =>
                                                {
                                                    await bucket.QuerySafeAsync<dynamic>($"drop index {name}");
                                                });

                                            context.WriteLine(AnsibleVerbosity.Trace, $"Dropped index [{name}].");
                                            context.WriteLine(AnsibleVerbosity.Trace, $"Recreating index [{name}].");

                                            await retry.InvokeAsync(
                                                async () =>
                                                {
                                                    await bucket.QuerySafeAsync<dynamic>(sbCreateIndexQuery.ToString());
                                                });

                                            context.WriteLine(AnsibleVerbosity.Info, $"Created index [{name}].");
                                        }
                                    }
                                }
                                else
                                {
                                    if (context.CheckMode)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Important, $"Index [{name}] will be created when CHECKMODE is disabled.");
                                    }
                                    else
                                    {
                                        context.Changed = true;

                                        context.WriteLine(AnsibleVerbosity.Trace, $"Creating index [{name}].");

                                        await retry.InvokeAsync(
                                            async () =>
                                            {
                                                await bucket.QuerySafeAsync<dynamic>(sbCreateIndexQuery.ToString());
                                            });

                                        context.WriteLine(AnsibleVerbosity.Info, $"Created index [{name}].");
                                    }
                                }
                                break;

                            case "absent":

                                if (existing != null)
                                {
                                    if (context.CheckMode)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Important, $"Index [{name}] will be dropped when CHECKMODE is disabled.");
                                    }
                                    else
                                    {
                                        context.Changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Dropping index [{name}].");

                                        await retry.InvokeAsync(
                                            async () =>
                                            {
                                                await bucket.QuerySafeAsync<dynamic>($"drop index {bucket} using {type}");
                                            });

                                        context.WriteLine(AnsibleVerbosity.Trace, $"Index [{name}] was dropped.");
                                    }
                                }
                                break;

                            default:

                                throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
                        }
                    }
                }).Wait();
        }
    }
}
