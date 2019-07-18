//-----------------------------------------------------------------------------
// FILE:	    Workflow.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Retry;
using Neon.Time;
using Neon.Diagnostics;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all application Cadence workflow implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cadence workflows are intended to implement the decision logic around zero
    /// or more activities that actually interact with the outside world or perform
    /// longer running computations.  You'll provide this logic in your <see cref="RunAsync(byte[])"/>
    /// method implementation.  This method accepts workflow parameters as byte array
    /// and returns the workflow result as another byte array (both of these arrays
    /// may also be <c>null</c>).
    /// </para>
    /// <para>
    /// Workflows are pretty easy to implement.  You'll need to derive your custom
    /// workflow class from <see cref="Workflow"/> and implement a default public
    /// constructor and then need to implement the <see cref="RunAsync(byte[])"/> method,
    /// which is where your workflow logic will reside.  
    /// </para>
    /// <note>
    /// <para>
    /// Workflow logic must be deterministic and idempotent:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     <para>
    ///     The code in your <see cref="RunAsync(byte[])"/> method must only rely on
    ///     state and data returned by Cadence methods for determining what to do.
    ///     This allows Cadence to replay previously completed workfow steps when
    ///     a workflow needs to be rescheduled on another worker.
    ///     </para>
    ///     <para>
    ///     This means that you must not call things like <see cref="DateTime.UtcNow"/>
    ///     directly in your workflow because this will likely return a different 
    ///     value every time it's called.  Instead, call  
    ///     </para>
    ///     </item>
    ///     <item>
    ///     <para>
    ///     Workflows are inherently single threaded.  You should never explicitly
    ///     create threads within <see cref="RunAsync(byte[])"/> or use things like
    ///     <see cref="Task.Run(Action)"/> which schedule work on background threads.
    ///     </para>
    ///     <note>
    ///     Workflows are allowed to run multiple activities in parallel and activities
    ///     can be multi-threaded, it's just the workflow code itself that can't use
    ///     threads because those will generally interfere with Cadence's ability to
    ///     replay workflow steps deterministically.
    ///     </note>
    ///     </item>
    ///     <item>
    ///     Workflows must never obtain the current time by using methods like 
    ///     <see cref="DateTime.UtcNow"/> directly.  Use <see cref="UtcNowAsync"/>
    ///     instead.
    ///     </item>
    ///     <item>
    ///     <para>
    ///     Cadence supports <b>workflow variables</b>.  Variables are identified by
    ///     non-empty string names and can reference byte array values or <c>null</c>.
    ///     You'll use <see cref="SetVariableAsync(string, byte[])"/> to set a variable
    ///     and <see cref="GetVariableAsync(string)"/> to retrieve a variable's value.
    ///     </para>
    ///     <note>
    ///     Uninitialized variables will return <c>null</c>.
    ///     </note>
    ///     <para>
    ///     Workflow variables are recorded in the history such that the consistent values
    ///     will be returned for each decision task when the workflow is replayed.  You
    ///     can use variables to hold non-deterministic or external state such as generated
    ///     UUIDs, random numbers, or the point-in-time state of an external system to
    ///     ensure that your workflows will make the same decisions when replayed.
    ///     </para>
    ///     </item>
    ///     <item>
    ///     Workflows should never call <see cref="Thread.Sleep(TimeSpan)"/> or 
    ///     <see cref="Task.Delay(TimeSpan)"/>.  Use <see cref="SleepAsync(TimeSpan)"/>
    ///     instead.
    ///     </item>
    /// </list>
    /// </note>
    /// <para>
    /// Here's an overview describing the steps necessary to implement, deploy, and
    /// start a workflow:
    /// </para>
    /// <list type="number">
    /// <item>
    ///     A custom workflow is implemented by deriving a class from <see cref="Workflow"/>,a
    ///     implementing the workflow logic via a <see cref="Workflow.RunAsync(byte[])"/>
    ///     method.  Any custom workflow activities will need to be implemented as classes
    ///     derived from <see cref="Activity"/>.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="Workflow"/> class needs to be deployed as a service or
    ///     application that creates a <see cref="CadenceClient"/> connected to a Cadence
    ///     cluster.  This application needs to call <see cref="CadenceClient.StartWorkflowWorkerAsync(string, string, WorkerOptions)"/>
    ///     and <see cref="CadenceClient.StartActivityWorkerAsync(string, string, WorkerOptions)"/> to
    ///     start the workflow and activity workers as required.
    ///     </para>
    /// </item>
    /// <item>
    ///     <para>
    ///     An external workflow instance can be started by calling <see cref="CadenceClient.StartWorkflowAsync(string, byte[], String, string, WorkflowOptions)"/>,
    ///     passing an optional byte array as workflow arguments as well as optional workflow options.  
    ///     External workflows have no parent, as opposed to child workflows that run in the context of 
    ///     another workflow (the parent).
    ///     </para>
    ///     <note>
    ///     <see cref="CadenceClient.StartWorkflowAsync(string, byte[], string, string, WorkflowOptions)"/> returns immediately
    ///     after the new workflow has been submitted to Cadence.  This method does not wait
    ///     for the workflow to finish.
    ///     </note>
    /// </item>
    /// <item>
    ///     For Neon Cadence client instances that have started a worker that handles the named workflow,
    ///     Cadence will choose one of the workers and begin executing the workflow there.  The Neon Cadence
    ///     client will instantiate the registered custom <see cref="Workflow"/> call its
    ///     <see cref="Workflow.RunAsync(byte[])"/> method, passing the optional workflow arguments
    ///     encoded as a byte array.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="Workflow.RunAsync(byte[])"/> method implements the workflow by
    ///     calling activities via <see cref="CallActivityAsync(string, byte[], ActivityOptions, CancellationToken)"/> or 
    ///     <see cref="CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions,  CancellationToken)"/> 
    ///     and child workflows via <see cref="CallChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken)"/>,
    ///     making decisions based on their results to call other activities and child workflows, 
    ///     and ultimately return a result or throwing an exception to indicate that the workflow
    ///     failed.
    ///     </para>
    ///     <para>
    ///     The Neon Cadence client expects workflow and activity parameters and results to be 
    ///     byte arrays or <c>null</c>.  It's up to the application to encode the actual values
    ///     into bytes using whatever encoding scheme that makes sense.  It is common though
    ///     to use the <see cref="NeonHelper.JsonSerialize(object, Formatting)"/> and
    ///     <see cref="NeonHelper.JsonDeserialize(Type, string, bool)"/> methods to serialize
    ///     parameters and results to JSON strings and then encode those as UTF-8 bytes.
    ///     </para>
    /// </item>
    /// <item>
    ///     <para>
    ///     Cadence also supports executing low overhead <b>local activities</b>.  These activities
    ///     are executed directly in the current process without needing to be scheduled by the
    ///     Cadence cluster and invoked on a worker.  Local activities are intended for tasks that
    ///     will execute quickly, on the order of a few seconds.
    ///     </para>
    ///     <para>
    ///     You'll use the <see cref="CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions, CancellationToken)"/>,
    ///     specifying your custom <see cref="Activity"/> implementation.
    ///     </para>
    ///     <note>
    ///     Local activity types do not need to be registered with a Cadence worker.
    ///     </note>
    ///     <para>
    ///     Local activities have some limitations:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///         Local activities cannot record Cadence heartbeats.
    ///         </item>
    ///         <item>
    ///         Local activity timeouts should be shorter than the decision task timeout
    ///         of the calling workflow.
    ///         </item>
    ///         <item>
    ///         The .NET Cadence client does not currently support cancellation of local activities.
    ///         </item>
    ///     </list>
    /// </item>
    /// <item>
    ///     <para>
    ///     Workflow instances can be signalled when external events occur via the 
    ///     <see cref="CadenceClient.SignalWorkflowAsync(string, string, byte[], string)"/> or
    ///     <see cref="CadenceClient.SignalWorkflowWithStartAsync(string, string, byte[], byte[], string, WorkflowOptions)"/>
    ///     methods.  Signals are identified by a string name and may include a byte
    ///     array payload.  Workflows receive signals by implementing a receive method
    ///     accepting a byte array payload parameter and tagging the method with a
    ///     <see cref="SignalMethodAttribute"/> specifying the signal name, like:
    ///     </para>
    ///     <code language="c#">
    ///     [SignalHandler("my-signal")]
    ///     protected async Task OnMySignal(byte[] args)
    ///     {
    ///         await DoDomethingAsync();
    ///     }
    ///     </code>
    ///     <note>
    ///     Exceptions thrown by signal handlers are caught and logged but are not
    ///     returned to the signaller.
    ///     </note>
    /// </item>
    /// <item>
    ///     <para>
    ///     Running workflows can also be queried via <see cref="CadenceClient.QueryWorkflowAsync(string, string, byte[], string)"/>.
    ///     Queries are identified by a name and may include optional arguments encoded 
    ///     as a byte array and return a result encoded as a byte array or <c>null</c>.
    ///     Workflows receive queries by implementing a receive method accepting the
    ///     query arguments as a byte array that returns the byte array result.  You'll
    ///     need to tag this with a <see cref="QueryMethodAttribute"/> specifying the
    ///     query name, like:
    ///     </para>
    ///     <code language="c#">
    ///     [QueryHandler("my-query")]
    ///     protected async Task&lt;byte[]&gt; OnMyQuery(byte[] args)
    ///     {
    ///         return await Task.FromResult(Encoding.UTF8.GetBytes("Hello World!"));
    ///     }
    ///     </code>
    ///     <note>
    ///     Exceptions thrown by query handlers are caught and will be returned to 
    ///     the caller to be thrown as an exception.
    ///     </note>
    /// </item>
    /// </list>
    /// <para><b>Child Workflows and Activities</b></para>
    /// <para>
    /// Workflows can run child workflows and activities.  Child workflows are started using
    /// <see cref="CallChildWorkflowAsync{TWorkflow}(byte[], ChildWorkflowOptions, CancellationToken)"/>,
    /// <see cref="CallChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken)"/>,
    ///  <see cref="StartChildWorkflowAsync{TWorkflow}(byte[], ChildWorkflowOptions, CancellationToken)"/>,
    ///  or <see cref="StartChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Activities are started via <see cref="CallActivityAsync{TActivity}(byte[], ActivityOptions, CancellationToken)"/>,
    /// or <see cref="CallActivityAsync(string, byte[], ActivityOptions, CancellationToken)"/>.
    /// </para>
    /// <para><b>Restarting Workflows</b></para>
    /// <para>
    /// Long running workflows that are essentially a loop can result in the recording
    /// of an excessive number of events to its history.  This can result in poor performance
    /// due to having to replay this history when the workflow has to be rehydrated.  
    /// </para>
    /// <para>
    /// You can avoid this by removing the workflow loop and calling <see cref="ContinueAsNew(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// at the end of your workflow logic.  This causes Cadence to reschedule the workflow
    /// with a clean history, somewhat similar to what happens for CRON workflows (which are
    /// rescheduled automatically).  <see cref="ContinueAsNew(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// works by throwing a <see cref="CadenceWorkflowRestartException"/> which will exit
    /// the workflow method and be caught by the calling <see cref="CadenceClient"/> which
    /// which then informs Cadence.
    /// </para>
    /// <note>
    /// Workflow entry points must allow the <see cref="CadenceWorkflowRestartException"/> to be caught by the
    /// calling <see cref="CadenceClient"/> so that <see cref="Workflow.ContinueAsNew(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// will work properly.
    /// </note>
    /// <para><b>Upgrading Workflows</b></para>
    /// <para>
    /// It is possible to upgrade workflow implementation with workflows in flight using
    /// the <see cref="GetVersionAsync(string, int, int)"/> method.  The essential requirement
    /// is that the new implementation must execute the same logic for the decision steps
    /// that have already been executed and recorded to the history fo a previous workflow 
    /// to maintain workflow determinism.  Subsequent unexecuted steps, are free to implement
    /// different logic.
    /// </para>
    /// <note>
    /// Cadence attempts to detect when replaying workflow performs actions that are different
    /// from those recorded as history and will fail the workflow when this occurs.
    /// </note>
    /// <para>
    /// Upgraded workflows will use <see cref="GetVersionAsync(string, int, int)"/> to indicate
    /// where upgraded logic has been inserted into the workflow.  You'll pass a <b>changeId</b>
    /// string that identifies the change being made.  This can be anything you wish as long as
    /// it's not empty and is unique for each change made to the workflow.  You'll also pass
    /// <b>minSupported</b> and <b>maxSupported</b> integers.  <b>minSupported</b> specifies the 
    /// minimum version of the workflow implementation that will be allowed to continue to
    /// run.  Workflows start out with their version set to <see cref="DefaultVersion"/>
    /// or <b>(-1)</b> and this will often be passed as <b>minSupported</b> such that upgraded
    /// workflow implementations will be able to take over newly scheduled workflows.  
    /// <b>maxSupported</b> essentially specifies the current version of the workflow 
    /// implementation. 
    /// </para>
    /// <para>
    /// When <see cref="GetVersionAsync(string, int, int)"/> called and is not being replayed
    /// from the workflow history, the method will record the <b>changeId</b> and <b>maxSupported</b>
    /// values to the workflow history.  When this is being replayed, the method will simply
    /// return the <b>maxSupported</b> value from the history.  Let's go through an example demonstrating
    /// how this can be used.  Let's say we start out with a simple two step workflow that 
    /// first calls <b>ActivityA</b> and then calls <b>ActivityB</b>:
    /// </para>
    /// <code lang="C#">
    /// public class MyWorkflow : WorkflowBase
    /// {
    ///     protected async Task&lt;byte[]&gt; RunAsync(byte[] args)
    ///     {
    ///         await CallActivity&lt;ActivityA&gt;();    
    ///         await CallActivity&lt;ActivityB&gt;();    
    /// 
    ///         return null;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Now, let's assume that we need to replace the call to <b>ActivityA</b> with a call to
    /// <b>ActivityC</b>.  If there is no chance of any instances of <B>MyWorkflow</B> being
    /// in flight, you could simply redepoy the recoded workflow:
    /// </para>
    /// <code lang="C#">
    /// public class MyWorkflow : WorkflowBase
    /// {
    ///     protected async Task&lt;byte[]&gt; RunAsync(byte[] args)
    ///     {
    ///         await CallActivity&lt;ActivityC&gt;();  // &lt;-- change
    ///         await CallActivity&lt;ActivityB&gt;();
    /// 
    ///         return null;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// But, if instances of this workflow are in flight you'll need to deploy a backwards
    /// compatible workflow implementation that handles workflows that have already executed 
    /// <b>ActivityA</b> but haven't yet executed <b>ActivityB</b>.  You can accomplish this
    /// via:
    /// </para>
    /// <code lang="C#">
    /// public class MyWorkflow : WorkflowBase
    /// {
    ///     protected async Task&lt;byte[]&gt; RunAsync(byte[] args)
    ///     {
    ///         var version = await GetVersionAsync("Replace ActivityA", DefaultVersion, 1);    
    /// 
    ///         switch (version)
    ///         {
    ///             case DefaultVersion:
    ///             
    ///                 await CallActivity&lt;ActivityA&gt;();
    ///                 break;
    ///                 
    ///             case 1:
    ///             
    ///                 await CallActivity&lt;ActivityC&gt;();  // &lt;-- change
    ///                 break;
    ///         }
    ///         
    ///         await CallActivity&lt;ActivityB&gt;();
    /// 
    ///         return null;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// This upgraded workflow calls <see cref="GetVersionAsync(string, int, int)"/> passing
    /// <b>minSupported=DefaultVersion</b> and <b>maxSupported=1</b>  For workflow instances
    /// that have already executed <b>ActivityA</b>, <see cref="GetVersionAsync(string, int, int)"/>
    /// will return <see cref="DefaultVersion"/> and we'll call <b>ActivityA</b>, which will match
    /// what was recorded in the history.  For workflows that have not yet executed <b>ActivityA</b>,
    /// <see cref="GetVersionAsync(string, int, int)"/> will return <b>1</b>, which we'll use as
    /// the indication that we can call <b>ActivityC</b>.
    /// </para>
    /// <para>
    /// Now, lets say we need to upgrade the workflow again and change the call for <b>ActivityB</b>
    /// to <b>ActivityD</b>, but only for workflows that have also executed <b>ActivityC</b>.  This 
    /// would look something like:
    /// </para>
    /// <code lang="C#">
    /// public class MyWorkflow : WorkflowBase
    /// {
    ///     protected async Task&lt;byte[]&gt; RunAsync(byte[] args)
    ///     {
    ///         var version = await GetVersionAsync("Replace ActivityA", DefaultVersion, 1);    
    /// 
    ///         switch (version)
    ///         {
    ///             case DefaultVersion:
    ///             
    ///                 await CallActivity&lt;ActivityA&gt;();
    ///                 break;
    ///                 
    ///             case 1:
    ///             
    ///                 await CallActivity&lt;ActivityC&gt;();  // &lt;-- change
    ///                 break;
    ///         }
    ///         
    ///         version = await GetVersionAsync("Replace ActivityB", 1, 2);    
    /// 
    ///         switch (version)
    ///         {
    ///             case DefaultVersion:
    ///             case 1:
    ///             
    ///                 await CallActivity&lt;ActivityB&gt;();
    ///                 break;
    ///                 
    ///             case 2:
    ///             
    ///                 await CallActivity&lt;ActivityD&gt;();  // &lt;-- change
    ///                 break;
    ///         }
    ///         
    ///         await CallActivity&lt;ActivityB&gt;();
    /// 
    ///         return null;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Notice that the second <see cref="GetVersionAsync(string, int, int)"/> call passed a different
    /// change ID and also that the version range is now <b>1..2</b>.  The version returned will be
    /// <see cref="DefaultVersion"/> or <b>1</b> if <b>ActivityA</b> and <b>ActivityB</b> were 
    /// recorded in the history or <b>2</b> if <b>ActivityC</b> was called.
    /// </para>
    /// </remarks>
    public abstract class Workflow : IWorkflow, INeonLogger
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to map a Cadence client ID and workflow context ID into a
        /// key that can be used to dereference <see cref="idToWorkflow"/>.
        /// </summary>
        private struct WorkflowKey
        {
            private long clientId;
            private long contextId;

            public WorkflowKey(CadenceClient client, long contextId)
            {
                this.clientId  = client.ClientId;
                this.contextId = contextId;
            }

            public override int GetHashCode()
            {
                return clientId.GetHashCode() ^ contextId.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is WorkflowKey))
                {
                    return false;
                }

                var other = (WorkflowKey)obj;

                return this.clientId == other.clientId && 
                       this.contextId == other.contextId;
            }

            public override string ToString()
            {
                return $"clientID={clientId}, contextId={contextId}";
            }
        }

        /// <summary>
        /// Private activity used to set/get variable values.  This activity simply
        /// returns the arguments passed such that they'll be recorded in the workflow
        /// history.  This is intended to be executed as a local activity.
        /// </summary>
        private class VariableActivity : Activity
        {
            protected override Task<byte[]> RunAsync(byte[] args)
            {
                return Task.FromResult(args);
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object                                   syncLock           = new object();
        private static INeonLogger                              log                = LogManager.Default.GetLogger<Workflow>();
        private static Dictionary<WorkflowKey, Workflow>        idToWorkflow       = new Dictionary<WorkflowKey, Workflow>();
        private static Dictionary<Type, WorkflowMethodMap>      typeToMethodMap    = new Dictionary<Type, WorkflowMethodMap>();

        // This dictionary is used to map workflow type names to the target workflow
        // type.  Note that these mappings are scoped to specific cadence client
        // instances by prefixing the type name with:
        //
        //      CLIENT-ID::
        //
        // where CLIENT-ID is the locally unique ID of the client.  This is important,
        // because we'll need to remove entries the for clients when they're disposed.

        private static Dictionary<string, Type>                 nameToWorkflowType = new Dictionary<string, Type>();

        /// <summary>
        /// The default workflow version returned by <see cref="GetVersionAsync(string, int, int)"/> 
        /// when a version has not been set yet.
        /// </summary>
        public int DefaultVersion = -1;

        /// <summary>
        /// Prepends the Cadence client ID to the workflow type name to generate the
        /// key used to dereference the <see cref="nameToWorkflowType"/> dictionary.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <returns>The prepended type name.</returns>
        private static string GetWorkflowTypeKey(CadenceClient client, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(workflowTypeName != null);

            return $"{client.ClientId}::{workflowTypeName}";
        }

        /// <summary>
        /// Registers a workflow type.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowType">The workflow type.</param>
        /// <param name="workflowTypeName">The name used to identify the implementation.</param>
        /// <returns><c>true</c> if the workflow was already registered.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different workflow class has already been registered for <paramref name="workflowTypeName"/>.</exception>
        internal static bool Register(CadenceClient client, Type workflowType, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(workflowType != null);
            Covenant.Requires<ArgumentException>(workflowType.IsSubclassOf(typeof(Workflow)), $"Type [{workflowType.FullName}] does not derive from [{nameof(Workflow)}]");
            Covenant.Requires<ArgumentException>(workflowType != typeof(Workflow), $"The base [{nameof(Workflow)}] class cannot be registered.");

            workflowTypeName = GetWorkflowTypeKey(client, workflowTypeName);

            lock (syncLock)
            {
                if (nameToWorkflowType.TryGetValue(workflowTypeName, out var existingEntry))
                {
                    if (!object.ReferenceEquals(existingEntry, workflowType))
                    {
                        throw new InvalidOperationException($"Conflicting workflow type registration: Workflow type [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                    }

                    return true;
                }
                else
                {
                    nameToWorkflowType[workflowTypeName] = workflowType;

                    return false;
                }
            }
        }

        /// <summary>
        /// Removes all type workflow type registrations for a Cadence client (when it's being disposed).
        /// </summary>
        /// <param name="client">The client being disposed.</param>
        internal static void UnregisterClient(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            var prefix = $"{client.ClientId}::";

            lock (syncLock)
            {
                foreach (var key in nameToWorkflowType.Keys.Where(key => key.StartsWith(prefix)).ToList())
                {
                    nameToWorkflowType.Remove(key);
                }
            }
        }

        /// <summary>
        /// Returns the .NET type implementing the named Cadence workflow.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowType">The Cadence workflow type string.</param>
        /// <returns>The workflow .NET type or <c>null</c> if the type was not found.</returns>
        private static Type GetWorkflowType(CadenceClient client, string workflowType)
        {
            Covenant.Requires<ArgumentNullException>(workflowType != null);

            lock (syncLock)
            {
                if (nameToWorkflowType.TryGetValue(GetWorkflowTypeKey(client, workflowType), out var type))
                {
                    return type;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Called to handle a workflow related request message received from the cadence-proxy.
        /// </summary>
        /// <param name="client">The client that received the request.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal static async Task OnProxyRequestAsync(CadenceClient client, ProxyRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.WorkflowInvokeRequest:

                    reply = await OnInvokeAsync(client, (WorkflowInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowSignalInvokeRequest:

                    reply = await OnSignalAsync(client, (WorkflowSignalInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryInvokeRequest:

                    reply = await OnQueryAsync(client, (WorkflowQueryInvokeRequest)request);
                    break;

                case InternalMessageTypes.ActivityInvokeLocalRequest:

                    reply = await OnInvokeLocalActivity(client, (ActivityInvokeLocalRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Thread-safe method that maps a workflow ID to the corresponding workflow instance.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <returns>The <see cref="Workflow"/> instance or <c>null</c> if the workflow was not found.</returns>
        private static Workflow GetWorkflow(CadenceClient client, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(new WorkflowKey(client, contextId), out var workflow))
                {
                    return workflow;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Handles workflow invocation.
        /// </summary>
        /// <param name="client">The associated cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowInvokeReply> OnInvokeAsync(CadenceClient client, WorkflowInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            Workflow    workflow;
            Type        workflowType;

            var contextId   = request.ContextId;
            var workflowKey = new WorkflowKey(client, contextId);

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(workflowKey, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow with [ID={workflowKey}] is already running on this worker.")
                    };
                }

                workflowType = GetWorkflowType(client, request.WorkflowType);

                if (workflowType == null)
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow [Type={request.WorkflowType}] is not registered for this worker.")
                    };
                }
            }

            workflow = (Workflow)Activator.CreateInstance(workflowType);

            workflow.Initialize(client, contextId);

            lock (syncLock)
            {
                idToWorkflow.Add(workflowKey, workflow);
            }

            // Initialize the other workflow properties.

            workflow.Client           = client;
            workflow.contextId        = request.ContextId;
            workflow.Domain           = request.Domain;
            workflow.RunId            = request.RunId;
            workflow.TaskList         = request.TaskList;
            workflow.WorkflowId       = request.WorkflowId;
            workflow.WorkflowTypeName = request.WorkflowType;

            // Register any workflow query or signal handlers.

            workflow.RegisterHandlers(client, contextId);

            // Start the workflow by calling its [RunAsync(args)] method.  This method will
            // indicate that it has completed via one of these techniques:
            //
            //      1. The method returns normally with the workflow result.
            //
            //      2. The method calls [RestartAsync(result, args)] which throws an
            //         [InternalWorkflowRestartException] which will be caught and
            //         handled here.
            //
            //      3. The method throws another exception which will be caught
            //         and be used to indicate that the workflow failed.

            try
            {
                var result = await workflow.RunAsync(request.Args);

                return new WorkflowInvokeReply()
                {
                    Result = result
                };
            }
            catch (CadenceWorkflowRestartException e)
            {
                return new WorkflowInvokeReply()
                {
                    ContinueAsNew                             = true,
                    ContinueAsNewArgs                         = e.Args,
                    ContinueAsNewDomain                       = e.Domain,
                    ContinueAsNewTaskList                     = e.TaskList,
                    ContinueAsNewExecutionStartToCloseTimeout = CadenceHelper.ToCadence(e.ExecutionStartToCloseTimeout),
                    ContinueAsNewScheduleToCloseTimeout       = CadenceHelper.ToCadence(e.ScheduleToCloseTimeout),
                    ContinueAsNewScheduleToStartTimeout       = CadenceHelper.ToCadence(e.ScheduleToStartTimeout),
                    ContinueAsNewStartToCloseTimeout          = CadenceHelper.ToCadence(e.StartToCloseTimeout),
                };
            }
            catch (CadenceException e)
            {
                return new WorkflowInvokeReply()
                {
                    Error = e.ToCadenceError()
                };
            }
            catch (Exception e)
            {
                return new WorkflowInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow signals.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowSignalInvokeReply> OnSignalAsync(CadenceClient client, WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.methodMap.GetSignalMethod(request.SignalName);

                    if (method != null)
                    {
                        await (Task)(method.Invoke(workflow, new object[] { request.SignalArgs }));

                        return new WorkflowSignalInvokeReply()
                        {
                            RequestId = request.RequestId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalInvokeReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    // It's possible that we'll land here if the workflow has been scheduled
                    // and/or started but execution has not actually started.  Since signals
                    // are fire-and-forget, we're just going to ignore these here.

                    return new WorkflowSignalInvokeReply();
                }
            }
            catch (Exception e)
            {
                return new WorkflowSignalInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow queries.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowQueryInvokeReply> OnQueryAsync(CadenceClient client, WorkflowQueryInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.methodMap.GetQueryMethod(request.QueryName);

                    if (method != null)
                    {
                        var result = await (Task<byte[]>)(method.Invoke(workflow, new object[] { request.QueryArgs }));

                        return new WorkflowQueryInvokeReply()
                        {
                            RequestId = request.RequestId,
                            Result    = result
                        };
                    }
                    else
                    {
                        return new WorkflowQueryInvokeReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a query handler for [queryName={request.QueryName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    return new WorkflowQueryInvokeReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new WorkflowQueryInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow local activity invocations.
        /// </summary>
        /// <param name="client">The client the request was received from.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<ActivityInvokeLocalReply> OnInvokeLocalActivity(CadenceClient client, ActivityInvokeLocalRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    Type activityType;

                    lock (syncLock)
                    {
                        if (!workflow.idToLocalActivityType.TryGetValue(request.ActivityTypeId, out activityType))
                        {
                            return new ActivityInvokeLocalReply()
                            {
                                Error = new CadenceEntityNotExistsException($"Activity type does not exist for [activityTypeId={request.ActivityTypeId}].").ToCadenceError()
                            };
                        }
                    }

                    var workerArgs = new WorkerArgs() { Client = client, ContextId = request.ActivityContextId };
                    var activity   = Activity.Create(client, activityType, null);
                    var result     = await activity.OnRunAsync(client, request.Args);

                    return new ActivityInvokeLocalReply()
                    {
                        Result = result
                    };
                }
                else
                {
                    return new ActivityInvokeLocalReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new ActivityInvokeLocalReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private long                        contextId;
        private WorkflowMethodMap           methodMap;
        private long                        nextLocalActivityTypeId;
        private Dictionary<long, Type>      idToLocalActivityType;
        private Dictionary<string, byte[]>  variables;
        private bool                        isDisconnected;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Workflow()
        {
        }

        /// <summary>
        /// Called internally to initialize the workflow.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        private void Initialize(CadenceClient client, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            this.Client                = client;
            this.contextId             = contextId;
            this.idToLocalActivityType = new Dictionary<long, Type>();
            this.variables             = new Dictionary<string, byte[]>();

            // Generate the signal/query method map for the workflow type if we
            // haven't already done that for this workflow type.

            var workflowType = this.GetType();

            lock (syncLock)
            {
                if (!typeToMethodMap.TryGetValue(workflowType, out methodMap))
                {
                    methodMap = WorkflowMethodMap.Create(workflowType);

                    typeToMethodMap.Add(workflowType, methodMap);
                }
            }
        }

        /// <summary>
        /// Called internally to register any workflow query and signal handlers.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        private void RegisterHandlers(CadenceClient client, long contextId)
        {
            // Register the query handlers with Cadence.

            foreach (var queryName in methodMap.GetQueryNames())
            {
                SetQueryHandlerAsync(queryName).Wait();
            }

            // Register the signal handlers with Cadence.

            foreach (var signalName in methodMap.GetSignalNames())
            {
                SignalSubscribeAsync(signalName).Wait();
            }
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        public CadenceClient Client { get; private set; }

        /// <summary>
        /// Returns the domain hosting the workflow.
        /// </summary>
        public string Domain { get; private set; }

        /// <summary>
        /// Returns the original workflow ID.
        /// </summary>
        public string WorkflowId { get; private set; }

        /// <summary>
        /// Returns the workflow's current run ID.
        /// </summary>
        public string RunId { get; private set; }

        /// <summary>
        /// Returns the workflow type name.
        /// </summary>
        public string WorkflowTypeName { get; private set; }

        /// <summary>
        /// Returns the task list where the workflow is executing.
        /// </summary>
        public string TaskList { get; private set; }

        /// <summary>
        /// Called by Cadence to execute a workflow.  Derived classes will need to implement
        /// their workflow logic here.
        /// </summary>
        /// <param name="args">The workflow arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The workflow result encoded as a byte array or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// There a several Cadence restrictions you need to keep in mind when implementing
        /// your workflow logic.  These are necessary so that Cadence will be able to
        /// transparently and deterministically replay previously completed workflow steps
        /// when workflows need to be restarted due to failures or other reasons.
        /// </para>
        /// <note>
        /// <para>
        /// Workflow logic must be deterministic and idempotent:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     <para>
        ///     The code in your <see cref="RunAsync(byte[])"/> method must only rely on
        ///     state and data returned by Cadence methods for determining what to do.
        ///     This allows Cadence to replay previously completed workfow steps when
        ///     a workflow needs to be rescheduled on another worker.
        ///     </para>
        ///     <para>
        ///     This means that you must not call things like <see cref="DateTime.UtcNow"/>
        ///     directly in your workflow because this will likely return a different 
        ///     value every time it's called.  Instead, call  
        ///     </para>
        ///     </item>
        ///     <item>
        ///     <para>
        ///     Workflows are inherently single threaded.  You should never explicitly
        ///     create threads within <see cref="RunAsync(byte[])"/> or use things like
        ///     <see cref="Task.Run(Action)"/> which schedule work on background threads.
        ///     </para>
        ///     <note>
        ///     Workflows are allowed to run multiple activities in parallel and activities
        ///     can be multi-threaded, it's just the workflow code itself that can't use
        ///     threads because those will generally interfere with Cadence's ability to
        ///     replay workflow steps deterministically.
        ///     </note>
        ///     </item>
        ///     <item>
        ///     Workflows must never obtain the current time by using methods like 
        ///     <see cref="DateTime.UtcNow"/> directly.  Use <see cref="UtcNowAsync"/>
        ///     instead.
        ///     </item>
        ///     <item>
        ///     Workflows should never directly query the environment where the workflow 
        ///     code is currently running.  This includes things like environment variables,
        ///     the machine host name or IP address, local files, etc.  You should generally
        ///     use activities for this or obtain this indirectly state using workflow
        ///     variables via <see cref="SetVariableAsync(string, byte[])"/> and 
        ///     <see cref="GetVariableAsync(string)"/>.  These methods ensure that Cadence 
        ///     will record the variable state in the workflow history such that the
        ///     same values will be returned when the workflow is replayed.
        ///     </item>
        ///     <item>
        ///     Workflows should never call <see cref="Thread.Sleep(TimeSpan)"/> or 
        ///     <see cref="Task.Delay(TimeSpan)"/>.  Use <see cref="SleepAsync(TimeSpan)"/>
        ///     or <see cref="SleepUntilUtcAsync(DateTime)"/> instead.
        ///     </item>
        /// </list>
        /// </note>
        /// </remarks>
        protected abstract Task<byte[]> RunAsync(byte[] args);

        /// <summary>
        /// Registers a query handler with Cadence.
        /// </summary>
        /// <param name="queryName">The query name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SetQueryHandlerAsync(string queryName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName));

            var reply = (WorkflowSetQueryHandlerReply)await Client.CallProxyAsync(
                new WorkflowSetQueryHandlerRequest()
                {
                    ContextId = this.contextId,
                    QueryName = queryName
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Subscribes a workflow to a signal.
        /// </summary>
        /// <param name="signalName">The signal name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SignalSubscribeAsync(string signalName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalSubscribeReply)await Client.CallProxyAsync(
                new WorkflowSignalSubscribeRequest()
                {
                    ContextId = this.contextId,
                    SignalName = signalName
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Used to implement backwards compatible changes to a workflow implementation.
        /// </summary>
        /// <param name="changeId">Identifies the change.</param>
        /// <param name="minSupported">
        /// Specifies the minimum supported version.  You may pass <see cref="DefaultVersion"/> <b>(-1)</b>
        /// which will be set as the version for workflows that haven't been versioned yet.
        /// </param>
        /// <param name="maxSupported">Specifies the maximum supported version.</param>
        /// <returns>The workflow implementation version.</returns>
        /// <remarks>
        /// See the <see cref="Workflow"/> remarks for more information about how this works.
        /// </remarks>
        protected async Task<int> GetVersionAsync(string changeId, int minSupported, int maxSupported)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(changeId));
            Covenant.Requires<ArgumentException>(minSupported <= maxSupported);

            var reply = (WorkflowGetVersionReply)await Client.CallProxyAsync(
                new WorkflowGetVersionRequest()
                {
                    ContextId    = this.contextId,
                    ChangeId     = changeId,
                    MinSupported = minSupported,
                    MaxSupported = maxSupported
                });

            reply.ThrowOnError();

            return reply.Version;
        }

        /// <summary>
        /// Returns <c>true</c> if there is a completion result from previous runs of
        /// this workflow.  This is useful for CRON workflows that would like to pass
        /// ending state from from one workflow execution to the next.  This property
        /// indicates whether the last execution (if any) returned any state.
        /// </summary>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<bool> HasPreviousRunResultAsync()
        {
            var reply = (WorkflowHasLastResultReply)await Client.CallProxyAsync(
                new WorkflowHasLastResultRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            return reply.HasResult;
        }

        /// <summary>
        /// Returns the result from the last workflow execution or <c>null</c>.  This is useful 
        /// for CRON workflows that would like to pass information from from one workflow
        /// execution to the next.
        /// </summary>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> GetPreviousRunResultAsync()
        {
            var reply = (WorkflowGetLastLastReply)await Client.CallProxyAsync(
                new WorkflowGetLastResultRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <summary>
        /// Called when a workflow has been cancelled and additional cleanup related work
        /// must be performed.  Calling this method allows the workflow to continue
        /// executing activities after the parent workflow has been cancelled.
        /// </summary>
        /// <remarks>
        /// Under the covers, this replaces the underlying workflow context with
        /// a new disconnected context that is independent from the parent workflow
        /// context.  This method only substitutes the new context for the first call. 
        /// Subsequent calls won't actually do anything.
        /// </remarks>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task DisconnectContextAsync()
        {
            if (isDisconnected)
            {
                // Already disconnected.

                return;
            }

            var reply = (WorkflowDisconnectContextReply)await Client.CallProxyAsync(
                new WorkflowDisconnectContextRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            isDisconnected = true;
        }

        /// <summary>
        /// Returns the current time (UTC).
        /// </summary>
        /// <returns>The current workflow time (UTC).</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<DateTime> UtcNowAsync()
        {
            var reply = (WorkflowGetTimeReply)await Client.CallProxyAsync(
                new WorkflowGetTimeRequest()
                {
                    ContextId = contextId
                });

            reply.ThrowOnError();

            return reply.Time;
        }

        /// <summary>
        /// Pauses the workflow for at least the period specified.
        /// </summary>
        /// <param name="duration">The time to sleep.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task SleepAsync(TimeSpan duration)
        {
            var reply = (WorkflowSleepReply)await Client.CallProxyAsync(
                new WorkflowSleepRequest()
                {
                    ContextId = contextId,
                    Duration  = duration
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Pauses the workflow at least until the specified time UTC.
        /// </summary>
        /// <param name="wakeTimeUtc">The time to sleep.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task SleepUntilUtcAsync(DateTime wakeTimeUtc)
        {
            var utcNow   = await UtcNowAsync();
            var duration = wakeTimeUtc - utcNow;

            if (duration <= TimeSpan.Zero)
            {
                // We're already at or past the requested time.

                return;
            }

            var reply = (WorkflowSleepReply)await Client.CallProxyAsync(
                new WorkflowSleepRequest()
                {
                    ContextId = contextId,
                    Duration  = duration
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// Exits and completes the current running workflow and then restarts it, passing the
        /// optional workflow arguments.
        /// </summary>
        /// <param name="args">Optional arguments for the new execution.</param>
        /// <param name="domain">Optional domain for the new execution.</param>
        /// <param name="taskList">Optional task list for the new execution.</param>
        /// <param name="executionToStartTimeout">Optional execution to start timeout for the new execution.</param>
        /// <param name="scheduleToCloseTimeout">Optional schedule to close timeout for the new execution.</param>
        /// <param name="scheduleToStartTimeout">Optional schedule to start timeout for the new execution.</param>
        /// <param name="startToCloseTimeout">Optional start to close timeout for the new execution.</param>
        /// <param name="retryOptions">Optional retry policy for the new execution.</param>
        /// <remarks>
        /// This works by throwing a <see cref="CadenceWorkflowRestartException"/> that will be
        /// caught and handled by the base <see cref="Workflow"/> class.    You'll need to allow
        /// this exception to exit your <see cref="RunAsync(byte[])"/> method for this to work.
        /// </remarks>
        protected async Task ContinueAsNew(
            byte[]          args                    = null,
            string          domain                  = null,
            string          taskList                = null,
            TimeSpan        executionToStartTimeout = default,
            TimeSpan        scheduleToCloseTimeout  = default,
            TimeSpan        scheduleToStartTimeout  = default,
            TimeSpan        startToCloseTimeout     = default,
            RetryOptions    retryOptions            = null)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            await Task.CompletedTask;

            // We're going to throw a [InternalWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new CadenceWorkflowRestartException(
                args:                       args,
                domain:                     domain,
                taskList:                   taskList,
                executionToStartTimeout:    executionToStartTimeout,
                scheduleToCloseTimeout:     scheduleToCloseTimeout,
                scheduleToStartTimeout:     scheduleToStartTimeout,
                startToCloseTimeout:        startToCloseTimeout,
                retryPolicy:                retryOptions);
        }

        //---------------------------------------------------------------------
        // Logging implementation

        // $todo(jeff.lill): Implement these.
        //
        // Note that these calls are all synchronous.  Perhaps we should consider dumping
        // the [INeonLogger] implementations in favor of simpler async methods?

        /// <inheritdoc/>
        public bool IsLogDebugEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogSInfoEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogInfoEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogWarnEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogErrorEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogSErrorEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogCriticalEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, IEnumerable<string> textFields, IEnumerable<double> numFields)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params string[] textFields)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params double[] numFields)
        {
        }
    }
}
