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

using Neon.Cryptography;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Hive;
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
        //                                                  be an IP address, a FQDN, hive
        //                                                  node name or hive node group name
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
        //                                      present     should be created or removed or
        //                                      build       whether any build deferred indexes
        //                                                  on the bucket should be built
        //
        // force        no          no          yes         optionally forces an index to be
        //                                      no          recreated when [state=present] when
        //                                                  the index already exists
        //
        // name         yes                                 the index name.  Required for non-primary
        //                                                  indexes and ignored for PRIMARY indexes.
        //
        // primary      no          no          yes         specifies a PRIMARY index
        //                                      no
        //
        // namespace    no          default                 specifies the index namespace.  This
        //                                                  defaults to the current bucket namespace.
        //
        // using        no          gsi         gsi         specifies the underlying index
        //                                      view        technology
        //
        // keys         see comment                         array of document fields, scalar
        //                                                  functions or array expressions to
        //                                                  be indexed.  This is required for
        //                                                  GSI indexes and must be empty
        //                                                  for the PRIMARY index.
        //
        // where        no                                  optionally specifies a WHERE clause
        //                                                  to create for a GSI or VIEW index
        //                                                  (see the WARNING in remarks below).
        //
        // nodes        no                                  optionally identifies the specific
        //                                                  index nodes where the index is to be
        //                                                  hosted.  This is an array of network
        //                                                  endpoints like "10.0.0.200:8091".
        //                                                  Ignored for VIEW indexes.
        //
        // replicas     no          [all]                   optionally specifies the number of
        //                                                  GSI based index replicas to deploy.  
        //                                                  Defaults to the number of Couchbase 
        //                                                  query nodes.  Ignored for VIEW indexes.
        //                                      
        // build_defer  no          no          yes         optionally defers creation of a GSI
        //                                      no          index until play with [state=build]
        //                                                  query is submitted.  See the remarks.
        //
        // build_wait   no          yes         yes         optionally avoid waiting for deferred
        //                                      no          indexes to be build by setting this
        //                                                  to FALSE.
        //
        // Remarks:
        // --------
        //
        // This module is used to manage Couchbase indexes.  Couchbase supports two basic
        // index types: PRIMARY and GSI (global secondary index).  A PRIMARY index must be
        // created to support N1QL queries and also to support GSI indexes.  PRIMARY indexes
        // may not include the [keys] or [where] parameters.
        //
        // Non-PRIMARY indexes must include at least one [keys] element specifying a document
        // property, scalar aggregate function or array expression.  You may also filter
        // these indexes by specifying a WHERE clause.
        //
        // GSI Indexes
        // -----------
        // By default, indexes will be hosted locally hosted within the hive buckets.
        // For more advanced environments, it's possible to host indexes on separate
        // index nodes for more efficent use of memory and often better query performance.
        //
        // The [nodes] and [replicas] module arguments can be used to control where
        // GSI indexes will be placed.  When neither of these are specified, this module
        // will distribute the index on all cluster nodes for fault tolerance.  You can
        // explicitly specify the number of index nodes by setting [replicas] to an integer
        // count.  You can also explicitly specify the index nodes by setting [nodes].
        //
        // Note that this module does not automatically relocate GSI indexes when the [nodes]
        // parameter or [replicas] parameter have changed.  You'll need to use [force=yes] 
        // to delete and recreate the indexes or do this manually.
        //
        // When creating multiple GSI indexes at the same time, you may want to specify
        // [build_defer=yes].  This creates the index but defers actually building it
        // until a separate [BUILD INDEX ...] query is executed, identifying a set of
        // GSI indexes to be built together.  This can be much more efficient because
        // only a single document scan will be required rather than performing a separate
        // scan for each index.
        //
        // After creating one or more deferred indexes, you can build them all with
        // another [neon_couchbase_index] play that sets [state=build].  This builds
        // all deferred indexes on the bucket and waits for this to complete by default.
        // Pass [build_wait=no] to kick of the builds without waiting.
        //
        // WHERE CLAUSE WARNING!
        // ---------------------
        // This module is not currently capable of parsing the [where] expression
        // so that it can converted to the canonical form that can be compared to the 
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
        //         specify that as the module [where] argument.
        //
        // This module will rebuild the index if the [condition] does not EXACTLY
        // match the [where] argument which is probably not what you want.
        //
        // Examples:
        // ---------
        //
        // This example creates a LOCAL PRIMARY index.
        //
        //  Equivalent to: 
        //
        //      CREATE PRIMARY INDEX ON test
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: primary index
        //        neon_couchbase_index:
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: present
        //          primary: yes
        //
        // This example creates a local secondary index on two document
        // keys: Name and Age.
        //
        //  Equivalent to:
        //
        //      CREATE INDEX idx_name_age ON test (Name, Age) 
        //          WITH { "nodes": ["127.0.0.1:8091"] }
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: secondary index
        //        neon_couchbase_index:
        //          nodes:
        //            - 10.0.0.30:8091
        //            - 10.0.0.31:8091
        //            - 10.0.0.32:8091
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
        //  Equivalent to: 
        //
        //      CREATE INDEX idx_seniors ON test (Name, Age) WHERE Age >= 65
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: secondary index
        //        neon_couchbase_index:
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
        //
        // This example creates two secondary GSI indexes, deferring the actual
        // index build and then builds the indexes together:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: name index
        //        neon_couchbase_index:
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: present
        //          name: idx_name
        //          keys:
        //            - Name
        //      - name: age index
        //        neon_couchbase_index:
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: present
        //          name: idx_age
        //          keys:
        //            - Age
        //      - name: build indexes
        //        neon_couchbase_index:
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          state: build

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

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "servers",
            "port",
            "ssl",
            "bucket",
            "username",
            "password",
            "state",
            "force",
            "name",
            "primary",
            "namespace",
            "using",
            "keys",
            "where",
            "nodes",
            "replicas",
            "build_defer",
            "build_wait"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive       = HiveHelper.Hive;
            var nodeGroups = hive.Definition.GetHostGroups(excludeAllGroup: true);

            //-----------------------------------------------------------------
            // Parse the module arguments.

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

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

            var primary = context.ParseBool("primary") ?? false;

            string name;

            if (primary)
            {
                name = "#primary";
            }
            else
            {
                name = context.ParseString("name", v => new Regex(@"[a-z][a-z0-0#_]*", RegexOptions.IgnoreCase).IsMatch(v));
            }

            if (string.IsNullOrEmpty(name) && state != "build")
            {
                context.WriteErrorLine("[name] argument is required.");
            }

            var type        = (context.ParseEnum<IndexType>("using") ?? default(IndexType)).ToString().ToUpperInvariant();
            var namespaceId = context.ParseString("namespace");

            if (string.IsNullOrEmpty(namespaceId))
            {
                namespaceId = "default";
            }

            var keys  = context.ParseStringArray("keys");
            var where = context.ParseString("where");
            var nodes = context.ParseStringArray("nodes");
            var defer = context.ParseBool("build_defer") ?? false;
            var wait  = context.ParseBool("build_wait") ?? true;

            var replicas = context.ParseInt("replicas");

            if (state == "present")
            {
                if (primary)
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

                if (type == "GSI" && replicas.HasValue && nodes.Count > 0)
                {
                    context.WriteErrorLine("Only one of [nodes] or [replicas] may be specified for GSI indexes.");
                    return;
                }
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

            if (context.HasErrors)
            {
                return;
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

                        var existingIndex = await bucket.GetIndexAsync(name);

                        if (existingIndex == null)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Index [{name}] does not exist.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Index [{name}] exists.");
                        }

                        var existingIsPrimary = existingIndex != null && existingIndex.IsPrimary;

                        switch (state.ToLowerInvariant())
                        {
                            case "present":

                                // Generate the index creation query.

                                var sbCreateIndexCommand = new StringBuilder();

                                if (primary)
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

                                if (!primary && !string.IsNullOrEmpty(where))
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

                                sbCreateIndexCommand.AppendWithSeparator($"using {type}");

                                // Append the WITH clause for GSI indexes.

                                if (type == "GSI")
                                {
                                    var sbWithSettings = new StringBuilder();

                                    if (defer)
                                    {
                                        sbWithSettings.AppendWithSeparator("\"defer_build\":true", ", ");
                                    }

                                    context.WriteLine(AnsibleVerbosity.Trace, "Query for the hive nodes.");

                                    var clusterNodes = await bucket.QuerySafeAsync<dynamic>("select nodes.name from system:nodes");

                                    context.WriteLine(AnsibleVerbosity.Trace, $"Hive has [{clusterNodes.Count}] nodes.");

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
                                            context.WriteErrorLine($"[replicas={replicas.Value}] cannot equal or exceed the number of Couchbase nodes.  [replicas={clusterNodes.Count - 1}] is the maximum allowed value for this hive.");
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

                                    if (replicas.HasValue && type == "GSI")
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

                                    var changed = false;

                                    if (force)
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, "Rebuilding index because [force=yes].");
                                    }
                                    // Compare the old/new index types.

                                    var orgType = existingIndex.Type.ToUpperInvariant();

                                    if (!string.Equals(orgType, type, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Rebuilding index because using changed from [{orgType}] to [{type}].");
                                    }

                                    // Compare the old/new index keys.

                                    var keysChanged = false;

                                    if (!primary)
                                    {
                                        var orgKeys = existingIndex.Keys;

                                        if (orgKeys.Length != keys.Count)
                                        {
                                            keysChanged = true;
                                        }
                                        else
                                        {
                                            // This assumes that the order of the indexed keys doesn't
                                            // matter.

                                            var keysSet = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);

                                            for (int i = 0; i < orgKeys.Length; i++)
                                            {
                                                keysSet[(string)orgKeys[i]] = false;
                                            }

                                            for (int i = 0; i < orgKeys.Length; i++)
                                            {
                                                keysSet[CbHelper.LiteralName(keys[i])] = true;
                                            }

                                            keysChanged = keysSet.Values.Count(k => !k) > 0;
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

                                    var orgWhere = existingIndex.Where;

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
                                            context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] will be rebuilt when CHECK-MODE is disabled.");
                                        }
                                        else
                                        {
                                            context.Changed = !context.CheckMode;

                                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing existing index [{indexId}].");
                                            string dropCommand;

                                            if (existingIsPrimary)
                                            {
                                                dropCommand = $"drop primary index on {CbHelper.LiteralName(bucket.Name)} using {orgType.ToUpperInvariant()}";
                                            }
                                            else
                                            {
                                                dropCommand = $"drop index {CbHelper.LiteralName(bucket.Name)}.{CbHelper.LiteralName(name)} using {orgType.ToUpperInvariant()}";
                                            }

                                            context.WriteLine(AnsibleVerbosity.Trace, $"DROP COMMAND: {dropCommand}");
                                            await bucket.QuerySafeAsync<dynamic>(dropCommand);
                                            context.WriteLine(AnsibleVerbosity.Trace, $"Dropped index [{indexId}].");

                                            context.WriteLine(AnsibleVerbosity.Trace, $"CREATE COMMAND: {sbCreateIndexCommand}");
                                            await bucket.QuerySafeAsync<dynamic>(sbCreateIndexCommand.ToString());
                                            context.WriteLine(AnsibleVerbosity.Info, $"Created index [{indexId}].");

                                            if (!defer && wait)
                                            {
                                                // Wait for the index to come online.

                                                context.WriteLine(AnsibleVerbosity.Info, $"Waiting for index [{name}] to be built.");
                                                await bucket.WaitForIndexAsync(name, "online");
                                                context.WriteLine(AnsibleVerbosity.Info, $"Completed building index [{name}].");
                                            }
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
                                        context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] will be created when CHECK-MODE is disabled.");
                                        context.WriteLine(AnsibleVerbosity.Trace, $"{sbCreateIndexCommand}");
                                    }
                                    else
                                    {
                                        context.Changed = true;

                                        context.WriteLine(AnsibleVerbosity.Trace, $"Creating index.");
                                        context.WriteLine(AnsibleVerbosity.Trace, $"CREATE COMMAND: {sbCreateIndexCommand}");
                                        await bucket.QuerySafeAsync<dynamic>(sbCreateIndexCommand.ToString());
                                        context.WriteLine(AnsibleVerbosity.Info, $"Created index [{indexId}].");

                                        if (!defer && wait)
                                        {
                                            // Wait for the index to come online.

                                            context.WriteLine(AnsibleVerbosity.Info, $"Waiting for index [{name}] to be built.");
                                            await bucket.WaitForIndexAsync(name, "online");
                                            context.WriteLine(AnsibleVerbosity.Info, $"Completed building index [{name}].");
                                        }
                                    }
                                }
                                break;

                            case "absent":

                                if (existingIndex != null)
                                {
                                    if (context.CheckMode)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] will be dropped when CHECK-MODE is disabled.");
                                    }
                                    else
                                    {
                                        context.Changed = true;
                                        context.WriteLine(AnsibleVerbosity.Info, $"Dropping index [{indexId}].");

                                        string orgType = existingIndex.Type;
                                        string dropCommand;

                                        if (existingIsPrimary)
                                        {
                                            dropCommand = $"drop primary index on {CbHelper.LiteralName(bucket.Name)} using {orgType.ToUpperInvariant()}";
                                        }
                                        else
                                        {
                                            dropCommand = $"drop index {CbHelper.LiteralName(bucket.Name)}.{CbHelper.LiteralName(name)} using {orgType.ToUpperInvariant()}";
                                        }

                                        context.WriteLine(AnsibleVerbosity.Trace, $"COMMAND: {dropCommand}");
                                        await bucket.QuerySafeAsync<dynamic>(dropCommand);
                                        context.WriteLine(AnsibleVerbosity.Trace, $"Index [{indexId}] was dropped.");
                                    }
                                }
                                else
                                {
                                    context.WriteLine(AnsibleVerbosity.Important, $"Index [{indexId}] does not exist so there's no need to drop it.");
                                }
                                break;

                            case "build":

                                // List the names of the deferred GSI indexes.

                                var deferredIndexes = ((await bucket.ListIndexesAsync()).Where(index => index.State == "deferred" && index.Type == "gsi")).ToList();

                                context.WriteLine(AnsibleVerbosity.Info, $"[{deferredIndexes.Count}] deferred GSI indexes exist.");

                                if (deferredIndexes.Count == 0)
                                {
                                    context.WriteLine(AnsibleVerbosity.Important, $"All GSI indexes have already been built.");
                                    context.Changed = false;
                                    return;
                                }

                                // Build the indexes (unless we're in CHECK-MODE).

                                var sbIndexList = new StringBuilder();

                                foreach (var deferredIndex in deferredIndexes)
                                {
                                    sbIndexList.AppendWithSeparator($"{CbHelper.LiteralName(deferredIndex.Name)}", ", ");
                                }

                                if (context.CheckMode)
                                {
                                    context.WriteLine(AnsibleVerbosity.Important, $"These GSI indexes will be built when CHECK-MODE is disabled: {sbIndexList}.");
                                    context.Changed = false;
                                    return;
                                }

                                var buildCommand = $"BUILD INDEX ON {CbHelper.LiteralName(bucket.Name)} ({sbIndexList})";

                                context.WriteLine(AnsibleVerbosity.Trace, $"BUILD COMMAND: {buildCommand}");
                                context.WriteLine(AnsibleVerbosity.Info, $"Building indexes: {sbIndexList}");
                                await bucket.QuerySafeAsync<dynamic>(buildCommand);
                                context.WriteLine(AnsibleVerbosity.Info, $"Build command submitted.");
                                context.Changed = true;

                                // The Couchbase BUILD INDEX command doesn't wait for the index
                                // building to complete so, we'll just spin until all of the
                                // indexes we're building are online.

                                if (wait)
                                {
                                    context.WriteLine(AnsibleVerbosity.Info, $"Waiting for the indexes to be built.");

                                    foreach (var deferredIndex in deferredIndexes)
                                    {
                                        await bucket.WaitForIndexAsync(deferredIndex.Name, "online");
                                    }
                                    
                                    context.WriteLine(AnsibleVerbosity.Info, $"Completed building [{deferredIndexes.Count}] indexes.");
                                }
                                break;

                            default:

                                throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [build].");
                        }
                    }

                }).Wait();
        }
    }
}
