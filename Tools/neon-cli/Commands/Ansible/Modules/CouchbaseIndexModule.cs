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
        // Manages Couchbase indexes. 
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
        //                                                  Couchbase nodes.  Each element can 
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
        // namespace    no          default                 specifies the index namespace.  This
        //                                                  defaults to the current bucket namespace.
        //
        // using        no          gsi         gsi         specifies the underlying index implementation
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
        // nodes        no                                  optionally identifies the specific
        //                                                  index nodes where the index is to be
        //                                                  hosted.  This is an array of network
        //                                                  endpoints like "node1:8091" or 
        //                                                  '10.0.0.200:8091".  This is ignored
        //                                                  for non-GSI indexes.
        //
        // replicas     no          see comment             optionally specifies the number of
        //                                                  GSI based index replicas to deploy.  
        //                                                  This defaults to 1.  This is ignored
        //                                                  for non-GSI indexes.
        //                                      
        // defer_build  no          no          yes         optionally defers creation of a GSI
        //                                                  index until a separate [BUILD INDEX...]
        //                                                  query is submitted.  See remarks.
        //
        // Remarks:
        // --------
        //
        // This module is used to manage Couchbase indexes.  Couchbase supports two basic
        // index types: PRIMARY and GSI (global secondary index).  A PRIMARY index must be
        // created to support N1QL queries and also to support GSI indexes.  PRIMARY indexes
        // may not include [keys] or [where] parameters.
        //
        // Non-primary indexes must include at least one [keys] element specifying a document
        // property, scalar aggregate function or array expression.  You may also filter
        // these indexes by specifying a WHERE clause.
        //
        // GSI Indexes
        // -----------
        // By default, indexes will be hosted locally hosted within the cluster buckets.
        // For more advanced environments, it's possible to host indexes on separate
        // index nodes for more efficent use of memory and often better query performance.
        //
        // The [nodes] and [replicas] module arguments can be used to control where
        // GSI indexes will be placed.  When neither of these are specified, this module
        // will host the index on all cluster nodes for fault tolerance.  You can explicitly
        // also specify the number of index nodes by setting [replicas] to an integer count.
        // You can also explicitly select the index nodes by setting [nodes].
        //
        // When creating multiple GSI indexes at the same time, you may want to specify
        // [defer_build=yes].  This creates the index but defers actually building it
        // until a separate [BUILD INDEX ...] query is executed, identifying a set of
        // GSI indexes to be built together.  This can be much more efficient because
        // only a single document scan will be required rather than performing a separate
        // scan for each index.
        //
        // Use the [neon_couchbase_query] module to submit a BUILD INDEX query to actually
        // build the pending indexes.
        //
        // Note that this module does not automatically relocate GSI indexes when the [nodes]
        // parameter has changed.  You'll need to use [force=yes] to delete and recreate
        // the indexes or do this manually.
        //
        // WHERE CLAUSE WARNING!
        // ---------------------
        //
        // This module is not currently capable of parsing the [where] expression
        // so that it can converted to the canonical form that will be returned as the 
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
        // This example creates a LOCAL primary index.
        //
        //  Equivalent to: CREATE PRIMARY INDEX idx_primary ON test
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: primary index
        //        neon_couchbase_index:
        //          nodes:
        //            - couchbase-0.mydomain.com
        //            - couchbase-1.mydomain.com
        //            - couchbase-2.mydomain.com
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: present
        //          name: idx_primary
        //          primary: yes
        //
        // This example creates a local secondary index on two document
        // keys: Name and Age.
        //
        //  Equivalent to: CREATE INDEX idx_name_age ON test (Name, Age)
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: secondary index
        //        neon_couchbase_index:
        //          nodes:
        //            - couchbase-0.mydomain.com
        //            - couchbase-1.mydomain.com
        //            - couchbase-2.mydomain.com
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: present
        //          name: idx_name_age
        //          keys:
        //            - Name
        //            - Age
        //
        // This example creates a local secondary index with a filtering
        // WHERE clause.
        //
        //  Equivalent to: CREATE INDEX idx_seniors ON test (Name, Age) where Age >= 65
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: secondary index
        //        neon_couchbase_index:
        //          nodes:
        //            - couchbase-0.mydomain.com
        //            - couchbase-1.mydomain.com
        //            - couchbase-2.mydomain.com
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: present
        //          name: idx_seniors
        //          keys:
        //            - Name
        //            - Age
        //          where: "(65 <= `Age`)"
        //
        // Note that the WHERE value exactly matches the [condition] property that
        // will be set for the index.  This was obtained by manually creating the
        // index first in toy cluster and then queried via:
        //
        //      SELECT * FROM system:indexes WHERE name = "idx_seniors"'
        //
        // The [where] parameter must EXACTLY MATCH what Couchbase is going to
        // set as the condition for index comparisons to work properly.

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

            var buildNow = context.ParseBool("build_all");

            if (!buildNow.HasValue)
            {
                buildNow = false;
            }

            var name = context.ParseString("name", v => new Regex(@"[a-z][a-z0-0#_]*", RegexOptions.IgnoreCase).IsMatch(v));

            var primary = context.ParseBool("primary");

            if (!primary.HasValue)
            {
                primary = false;
            }

            var type = context.ParseEnum<IndexType>("using");

            if (!type.HasValue)
            {
                type = default(IndexType);
            }

            var namespaceId = context.ParseString("namespace");

            if (string.IsNullOrEmpty(namespaceId))
            {
                namespaceId = "default";
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
                    context.WriteErrorLine("PRIMARY indexes do not allow any [keys].");
                    return;
                }

                if (!string.IsNullOrEmpty(where))
                {
                    context.WriteErrorLine("PRIMARY indexes do not support the [where] clause.");
                    return;
                }
            }
            else
            {
                if (keys.Count == 0)
                {
                    context.WriteErrorLine("Non-PRIMARY indexes must specify at least one [key].");
                    return;
                }
            }

            if (type.Value == IndexType.Gsi && replicas.HasValue && nodes.Count > 0)
            {
                context.WriteErrorLine("Only one of [nodes] or [replicas] may be specified for a GSI index.");
                return;
            }

            string keyspace;

            if (!string.IsNullOrEmpty(namespaceId) && namespaceId != "default")
            {
                keyspace = $"{namespaceId}:{couchbaseArgs.Settings.Bucket}";
            }
            else
            {
                keyspace = couchbaseArgs.Settings.Bucket;
            }

            //-----------------------------------------------------------------
            // Perform the operation.

            Task.Run(
                async () =>
                {
                    using (var bucket = couchbaseArgs.Settings.OpenBucket(couchbaseArgs.Credentials))
                    {
                        var indexId = $"{bucket.Name}.{name}";

                        // Fetch the index if it already exists.

                        var existingQuery = $"select * from system:indexes where keyspace_id={CbHelper.Literal(bucket.Name)} and namespace_id={CbHelper.Literal(namespaceId)} and name={CbHelper.Literal(name)}";

                        context.WriteLine(AnsibleVerbosity.Trace, $"Index query: {existingQuery}");

                        var existingIndex = (await bucket.QuerySafeAsync<JObject>(existingQuery)).FirstOrDefault();

                        switch (state.ToLowerInvariant())
                        {
                            case "present":

                                // Generate the index creation query.

                                var sbCreateIndexCommand = new StringBuilder();

                                if (primary.Value)
                                {
                                    sbCreateIndexCommand.Append($"create primary index {CbHelper.LiteralName(name)} on {CbHelper.LiteralName(bucket.Name)}");
                                }
                                else
                                {
                                    var sbKeys = new StringBuilder();

                                    foreach (var key in keys)
                                    {
                                        sbKeys.AppendWithSeparator(key, ", ");
                                    }

                                    sbCreateIndexCommand.Append($"create index {CbHelper.LiteralName(name)} on {CbHelper.LiteralName(bucket.Name)} ( {sbKeys} )");
                                }

                                // Append the WHERE clause for non-PRIMARY indexes.

                                if (!primary.Value && !string.IsNullOrEmpty(where))
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

                                    sbCreateIndexCommand.AppendWithSeparator($"where {queryWhere}");
                                }

                                // Append the USING clause.

                                sbCreateIndexCommand.AppendWithSeparator($"using {type.ToString().ToLowerInvariant()}");

                                // Append the WITH clause for GSI indexes.

                                if (type.Value == IndexType.Gsi)
                                {
                                    var sbWithSettings = new StringBuilder();

                                    if (defer.Value && type.Value == IndexType.Gsi)
                                    {
                                        sbWithSettings.AppendWithSeparator("\"defer_build\":true", ", ");
                                    }

                                    context.WriteLine(AnsibleVerbosity.Trace, "Query for the cluster nodes.");

                                    var clusterNodes = await bucket.QuerySafeAsync<dynamic>("select nodes.name from system:nodes");

                                    context.WriteLine(AnsibleVerbosity.Trace, $"Cluster has [{clusterNodes.Count}] nodes.");

                                    if ((!replicas.HasValue || replicas.Value == 0) && nodes.Count == 0)
                                    {
                                        // We're going to default to hosting GSI indexes explicitly 
                                        // on all nodes unless directed otherwise.  We'll need query
                                        // the database for the current nodes.

                                        foreach (JObject node in clusterNodes)
                                        {
                                            nodes.Add((string)node.GetValue("name"));
                                        }
                                    }
                                    else if (replicas.HasValue && replicas.Value > 0)
                                    {
                                        if (clusterNodes.Count <= replicas.Value)
                                        {
                                            context.WriteErrorLine($"[replicas={replicas.Value}] cannot equal or exceed the number of Couchbase nodes.  [replicas={clusterNodes.Count - 1}] is the maximum allowed value for this cluster.");
                                            return;
                                        }
                                    }

                                    if (nodes.Count > 0)
                                    {
                                        sbWithSettings.AppendWithSeparator("\"nodes\": [", ", ");

                                        var first = true;

                                        foreach (var server in nodes)
                                        {
                                            if (first)
                                            {
                                                first = false;
                                            }
                                            else
                                            {
                                                sbCreateIndexCommand.Append(",");
                                            }

                                            sbWithSettings.Append(CbHelper.Literal(server));
                                        }

                                        sbWithSettings.Append("]");
                                    }

                                    if (replicas.HasValue && type.Value == IndexType.Gsi)
                                    {
                                        sbWithSettings.AppendWithSeparator($"\"num_replica\":{CbHelper.Literal(replicas.Value)}", ", ");
                                    }

                                    if (sbWithSettings.Length > 0)
                                    {
                                        sbCreateIndexCommand.AppendWithSeparator($"with {{ {sbWithSettings} }}");
                                    }
                                }

                                // Add or update the index.

                                if (existingIndex != null)
                                {
                                    context.WriteLine(AnsibleVerbosity.Info, $"Index [{indexId}] already exists.");

                                    // An index with this name already exists, so we'll compare its
                                    // properties with the module parameters to determine whether we
                                    // need to remove and recreate it.

                                    var index   = (JObject)existingIndex["indexes"];
                                    var changed = false;

                                    if (force)
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, "Rebuilding index because [force=yes].");
                                    }

                                    // Determine if the index is being changed to/from a primary index.

                                    if (!index.TryGetValue<bool>("is_primary", out var existingIsPrimary))
                                    {
                                        existingIsPrimary = false;
                                    }

                                    if (primary.Value != existingIsPrimary)
                                    {
                                        changed = true;

                                        if (primary.Value)
                                        {
                                            context.WriteLine(AnsibleVerbosity.Info, "Rebuilding index because it is becoming a SECONDARY index.");
                                        }
                                        else
                                        {
                                            context.WriteLine(AnsibleVerbosity.Info, "Rebuilding index because it is becoming a PRIMARY index.");
                                        }
                                    }

                                    // Compare the old/new index types.

                                    var orgType = (string)index["using"];

                                    if (!string.Equals(orgType, type.ToString(), StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Rebuilding index because using changed from [{orgType.ToString().ToUpperInvariant()}] to [{type.ToString().ToUpperInvariant()}].");
                                    }

                                    // Compare the old/new index keys.

                                    var keysChanged = false;

                                    if (!primary.Value)
                                    {
                                        var orgKeys = (JArray)index["index_key"];

                                        if (orgKeys.Count != keys.Count)
                                        {
                                            keysChanged = true;
                                        }
                                        else
                                        {
                                            // $todo(jeff.lill):
                                            //
                                            // This assumes that the order of the indexed keys matters.
                                            // This might not be the case for Couchbase.  If the order
                                            // doesn't matter, we could avoid unnecessary index rebuilds
                                            // by doing a better check.

                                            for (int i = 0; i < orgKeys.Count; i++)
                                            {
                                                if ((string)orgKeys[i] != CbHelper.LiteralName(keys[i]))
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

                                            context.WriteLine(AnsibleVerbosity.Info, $"Rebuilding index because keys changed from [{sbOrgKeys}] to [{sbNewKeys}].");
                                        }
                                    }

                                    // Compare the filter condition.

                                    var orgWhere = (string)index["condition"];

                                    if (orgWhere != where)
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Rebuilding index because where clause changed from [{orgWhere ?? string.Empty}] to [{where ?? string.Empty}].");
                                    }

                                    // We need to remove and recreate the index if it differs
                                    // from what was requested.

                                    if (changed)
                                    {
                                        if (context.CheckMode)
                                        {
                                            context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] will be rebuilt when CHECKMODE is disabled.");
                                            context.WriteLine(AnsibleVerbosity.Trace, $"Create command: {sbCreateIndexCommand}");
                                        }
                                        else
                                        {
                                            context.Changed = true;

                                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing existing index [{indexId}].");

                                            var dropCommand = $"drop index {CbHelper.LiteralName(bucket.Name)}.{CbHelper.LiteralName(name)} using {orgType.ToString().ToUpperInvariant()}";

                                            context.WriteLine(AnsibleVerbosity.Trace, $"Dropping index via: {dropCommand}");
                                            await bucket.QuerySafeAsync<dynamic>(dropCommand);
                                            context.WriteLine(AnsibleVerbosity.Trace, $"Dropped index [{indexId}].");

                                            context.WriteLine(AnsibleVerbosity.Trace, $"Rebuilding index via: {sbCreateIndexCommand}");
                                            await bucket.QuerySafeAsync<dynamic>(sbCreateIndexCommand.ToString());
                                            context.WriteLine(AnsibleVerbosity.Info, $"Created index [{indexId}].");
                                        }
                                    }
                                    else
                                    {
                                        context.WriteLine(AnsibleVerbosity.Trace, $"No changes detected for index [{indexId}].");
                                    }
                                }
                                else
                                {
                                    if (context.CheckMode)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] will be created when CHECKMODE is disabled.");
                                        context.WriteLine(AnsibleVerbosity.Trace, $"{sbCreateIndexCommand}");

                                    }
                                    else
                                    {
                                        context.Changed = true;

                                        context.WriteLine(AnsibleVerbosity.Trace, $"Creating index via: {sbCreateIndexCommand}");
                                        await bucket.QuerySafeAsync<dynamic>(sbCreateIndexCommand.ToString());
                                        context.WriteLine(AnsibleVerbosity.Info, $"Created index [{indexId}].");
                                    }
                                }
                                break;

                            case "absent":

                                if (existingIndex != null)
                                {
                                    if (context.CheckMode)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] will be dropped when CHECKMODE is disabled.");
                                    }
                                    else
                                    {
                                        context.Changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Dropping index [{indexId}].");
                                        await bucket.QuerySafeAsync<dynamic>($"drop index {CbHelper.LiteralName(bucket.Name)}.{CbHelper.LiteralName(name)} using {type}");
                                        context.WriteLine(AnsibleVerbosity.Trace, $"Index [{indexId}] was dropped.");
                                    }
                                }
                                else
                                {
                                    context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] does not exist so there's no need to drop it.");
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
