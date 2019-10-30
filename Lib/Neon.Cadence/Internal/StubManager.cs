//-----------------------------------------------------------------------------
// FILE:	    StubManager.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime;
using System.Runtime.Loader;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using System.IO;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Manages the dynamic generation of workflow and activity stub classes.
    /// </summary>
    internal static class StubManager
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Describes what a workflow interface method does.
        /// </summary>
        private enum WorkflowMethodKind
        {
            /// <summary>
            /// The method implements a query.
            /// </summary>
            Query,

            /// <summary>
            /// The method implements a signal.
            /// </summary>
            Signal,

            /// <summary>
            /// The method is a workflow entry point.
            /// </summary>
            Workflow
        }

        /// <summary>
        /// Holds additional information about a workflow interface method.
        /// </summary>
        private class WorkflowMethodDetails
        {
            /// <summary>
            /// The workflow method type.
            /// </summary>
            public WorkflowMethodKind Kind { get; set; }

            /// <summary>
            /// The signal attributes for signal methods
            /// </summary>
            public SignalMethodAttribute SignalMethodAttribute { get; set; }

            /// <summary>
            /// The query attributes for query methods.
            /// </summary>
            public QueryMethodAttribute QueryMethodAttribute { get; set; }

            /// <summary>
            /// The workflow attributes for workflow methods.
            /// </summary>
            public WorkflowMethodAttribute WorkflowMethodAttribute { get; set; }

            /// <summary>
            /// Indicates whether the workflow result is <see cref="void"/>.
            /// </summary>
            public bool IsVoid => ReturnType == typeof(void);

            /// <summary>
            /// The workflow result type, not including the wrapping <see cref="Task"/>.
            /// This will be <see cref="void"/> for methods that don't return a value.
            /// </summary>
            public Type ReturnType { get; set; }

            /// <summary>
            /// The low-level method information.
            /// </summary>
            public MethodInfo Method { get; set; }
        }

        /// <summary>
        /// Holds additional information about an activity method.
        /// </summary>
        private class ActivityMethodDetails
        {
            /// <summary>
            /// The activity method attribute (or <c>null</c>).
            /// </summary>
            public ActivityMethodAttribute ActivityMethodAttribute { get; set; }

            /// <summary>
            /// Indicates whether the activity result is <see cref="void"/>.
            /// </summary>
            public bool IsVoid => ReturnType == typeof(void);

            /// <summary>
            /// The activity result type, not including the wrapping <see cref="Task"/>.
            /// This will be <see cref="void"/> for methods that don't return a value.
            /// </summary>
            public Type ReturnType { get; set; }

            /// <summary>
            /// The low-level method information.
            /// </summary>
            public MethodInfo Method { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static int      nextClassId = -1;
        private static object   syncLock    = new object();

        // These dictionaries map workflow interfaces to their dynamically generated stubs
        // for external and child workflows.

        private static Dictionary<Type, DynamicWorkflowStub> workflowInterfaceToStub      = new Dictionary<Type, DynamicWorkflowStub>();
        private static Dictionary<Type, DynamicWorkflowStub> workflowInterfaceToChildStub = new Dictionary<Type, DynamicWorkflowStub>();

        // ...and this one does the same for activities.

        private static Dictionary<Type, DynamicActivityStub> activityInterfaceToStub = new Dictionary<Type, DynamicActivityStub>();

        /// <summary>
        /// Returns C# compatible fully qualified type name for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The fully qualified type name.</returns>
        private static string NormalizeTypeName(Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));

            // .NET returns fully qualified type names that include a "+" for
            // nested types.  We're going to convert these to "." to make the 
            // name's C# compatible.  We're also going to prepend "global::"
            // to avoid namespace conflicts.

            return "global::" + type.FullName.Replace('+', '.');
        }

        /// <summary>
        /// Generates the static <b>___StubHelper</b> class that exposes internal methods
        /// and constructors from <see cref="CadenceClient"/> and other types that are
        /// required by generated workflow and activity stubs.  The generated class uses
        /// reflection to gain access these members.
        /// </summary>
        /// <param name="sbSource">The builder used to generated C# source.</param>
        private static void AppendStubHelper(StringBuilder sbSource)
        {
            sbSource.Append(
@"        private static class ___StubHelper
        {
            private static MethodInfo       startWorkflowAsync;                     // from: CadenceClient
            private static MethodInfo       getWorkflowResultAsync;                 // from: CadenceClient
            private static MethodInfo       startChildWorkflowAsync;                // from: CadenceClient
            private static MethodInfo       getChildWorkflowResultAsync;            // from: CadenceClient
            private static MethodInfo       describeWorkflowAsync;                  // from: CadenceClient
            private static MethodInfo       cancelWorkflowAsync;                    // from: CadenceClient
            private static MethodInfo       terminateWorkflowAsync;                 // from: CadenceClient
            private static MethodInfo       signalWorkflowAsync;                    // from: CadenceClient
            private static MethodInfo       signalWorkflowWithStartAsync;           // from: CadenceClient
            private static MethodInfo       signalChildWorkflowAsync;               // from: CadenceClient
            private static MethodInfo       queryWorkflowAsync;                     // from: CadenceClient
            private static MethodInfo       resolveDomain;                          // from: CadenceClient
            private static MethodInfo       newWorkflowStub;                        // from: CadenceClient
            private static MethodInfo       executeActivityAsync;                   // from: Workflow
            private static MethodInfo       executeLocalActivityAsync;              // from: Workflow
            private static MethodInfo       activityOptionsNormalize;               // from: ActivityOptions
            private static MethodInfo       localActivityOptionsNormalize;          // from: LocalActivityOptions
            private static MethodInfo       childWorkflowOptionsNormalize;          // from: ChildWorkflowOptions
            private static MethodInfo       childWorkflowOptionsToWorkflowOptions;  // from: ChildWorkflowOptions
            private static MethodInfo       workflowOptionsNormalize;               // from: WorkflowOptions
            private static ConstructorInfo  workflowStubConstructor;                // from: WorkflowStub

            static ___StubHelper()
            {
                var clientType   = typeof(CadenceClient);
                var workflowType = typeof(Workflow);

                startWorkflowAsync                    = NeonHelper.GetMethod(clientType, ""StartWorkflowAsync"", typeof(string), typeof(byte[]), typeof(WorkflowOptions));
                getWorkflowResultAsync                = NeonHelper.GetMethod(clientType, ""GetWorkflowResultAsync"", typeof(WorkflowExecution), typeof(string));
                startChildWorkflowAsync               = NeonHelper.GetMethod(clientType, ""StartChildWorkflowAsync"", typeof(Workflow), typeof(string), typeof(byte[]), typeof(ChildWorkflowOptions));
                getChildWorkflowResultAsync           = NeonHelper.GetMethod(clientType, ""GetChildWorkflowResultAsync"", typeof(Workflow), typeof(ChildExecution));
                describeWorkflowAsync                 = NeonHelper.GetMethod(clientType, ""DescribeWorkflowAsync"", typeof(WorkflowExecution), typeof(string));
                cancelWorkflowAsync                   = NeonHelper.GetMethod(clientType, ""CancelWorkflowAsync"", typeof(WorkflowExecution), typeof(string));
                terminateWorkflowAsync                = NeonHelper.GetMethod(clientType, ""TerminateWorkflowAsync"", typeof(WorkflowExecution), typeof(string), typeof(byte[]), typeof(string));
                signalWorkflowAsync                   = NeonHelper.GetMethod(clientType, ""SignalWorkflowAsync"", typeof(WorkflowExecution), typeof(string), typeof(byte[]), typeof(string));
                signalWorkflowWithStartAsync          = NeonHelper.GetMethod(clientType, ""SignalWorkflowWithStartAsync"", typeof(string), typeof(string), typeof(byte[]), typeof(byte[]), typeof(WorkflowOptions));
                signalChildWorkflowAsync              = NeonHelper.GetMethod(clientType, ""SignalChildWorkflowAsync"", typeof(Workflow), typeof(ChildExecution), typeof(string), typeof(byte[]));
                queryWorkflowAsync                    = NeonHelper.GetMethod(clientType, ""QueryWorkflowAsync"", typeof(WorkflowExecution), typeof(string), typeof(byte[]), typeof(string));
                resolveDomain                         = NeonHelper.GetMethod(clientType, ""ResolveDomain"", typeof(string));
                newWorkflowStub                       = NeonHelper.GetMethod(clientType, ""NewWorkflowStub"", typeof(string), typeof(WorkflowOptions));
                executeActivityAsync                  = NeonHelper.GetMethod(workflowType, ""ExecuteActivityAsync"", typeof(string), typeof(byte[]), typeof(ActivityOptions));
                executeLocalActivityAsync             = NeonHelper.GetMethod(workflowType, ""ExecuteLocalActivityAsync"", typeof(Type), typeof(ConstructorInfo), typeof(MethodInfo), typeof(byte[]), typeof(LocalActivityOptions));
                activityOptionsNormalize              = NeonHelper.GetMethod(typeof(ActivityOptions), ""Normalize"", typeof(CadenceClient), typeof(ActivityOptions), typeof(System.Type));
                localActivityOptionsNormalize         = NeonHelper.GetMethod(typeof(LocalActivityOptions), ""Normalize"", typeof(CadenceClient), typeof(LocalActivityOptions));
                childWorkflowOptionsNormalize         = NeonHelper.GetMethod(typeof(ChildWorkflowOptions), ""Normalize"", typeof(CadenceClient), typeof(ChildWorkflowOptions), typeof(System.Type));
                childWorkflowOptionsToWorkflowOptions = NeonHelper.GetMethod(typeof(ChildWorkflowOptions), ""ToWorkflowOptions"");
                workflowOptionsNormalize              = NeonHelper.GetMethod(typeof(WorkflowOptions), ""Normalize"", typeof(CadenceClient), typeof(WorkflowOptions), typeof(System.Type));
                workflowStubConstructor               = NeonHelper.GetConstructor(typeof(WorkflowStub), new Type[] { typeof(CadenceClient), typeof(string), typeof(WorkflowExecution), typeof(WorkflowOptions) });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<WorkflowExecution> StartWorkflowAsync(CadenceClient client, string workflowTypeName, byte[] args, WorkflowOptions options)
            {
                return await (Task<WorkflowExecution>)startWorkflowAsync.Invoke(client, new object[] { workflowTypeName, args, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> GetWorkflowResultAsync(CadenceClient client, WorkflowExecution execution, string domain)
            {
                return await (Task<byte[]>)getWorkflowResultAsync.Invoke(client, new object[] { execution, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<ChildExecution> StartChildWorkflowAsync(CadenceClient client, Workflow parentWorkflow, string workflowTypeName, byte[] args, ChildWorkflowOptions options)
            {
                return await (Task<ChildExecution>)startChildWorkflowAsync.Invoke(client, new object[] { parentWorkflow, workflowTypeName, args, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> GetChildWorkflowResultAsync(CadenceClient client, Workflow parentWorkflow, ChildExecution execution)
            {
                return await (Task<byte[]>)getChildWorkflowResultAsync.Invoke(client, new object[] { parentWorkflow, execution });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<WorkflowDescription> DescribeWorkflowAsync(CadenceClient client, WorkflowExecution execution, string domain)
            {
                return await (Task<WorkflowDescription>)describeWorkflowAsync.Invoke(client, new object[] { execution, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task CancelWorkflowAsync(CadenceClient client, WorkflowExecution execution, string domain)
            {
                await (Task)cancelWorkflowAsync.Invoke(client, new object[] { execution, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task TerminateWorkflowAsync(CadenceClient client, WorkflowExecution execution, string reason, byte[] details, string domain)
            {
                await (Task)terminateWorkflowAsync.Invoke(client, new object[] { execution, reason, details, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task SignalWorkflowAsync(CadenceClient client, WorkflowExecution execution, string signalName, byte[] signalArgs, string domain)
            {
                await (Task)signalWorkflowAsync.Invoke(client, new object[] { execution, signalName, signalArgs, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task SignalWorkflowWithStartAsync(CadenceClient client, string workflowTypeName, string signalName, byte[] signalArgs, byte[] workflowArgs, WorkflowOptions options)
            {
                await (Task)signalWorkflowWithStartAsync.Invoke(client, new object[] { workflowTypeName, signalName, signalArgs, workflowArgs, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> QueryWorkflowAsync(CadenceClient client, WorkflowExecution execution, string queryType, byte[] queryArgs, string domain)
            {
                return await (Task<byte[]>)queryWorkflowAsync.Invoke(client, new object[] { execution, queryType, queryArgs, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string ResolveDomain(CadenceClient client, string domain)
            {
                return (string)resolveDomain.Invoke(client, new object[] { domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static WorkflowStub NewWorkflowStub(CadenceClient client, string workflowTypeName, WorkflowOptions options)
            {
                return (WorkflowStub)newWorkflowStub.Invoke(client, new object[] { workflowTypeName, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> ExecuteActivityAsync(Workflow workflow, string activityTypeName, byte[] args, ActivityOptions options)
            {
                return await (Task<byte[]>)executeActivityAsync.Invoke(workflow, new object[] { activityTypeName, args, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task SignalChildWorkflowAsync(CadenceClient client, Workflow workflow, ChildExecution child, string signalName, byte[] signalArgs)
            {
                await (Task)signalChildWorkflowAsync.Invoke(client, new object[] { workflow, child, signalName, signalArgs });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> ExecuteLocalActivityAsync(Workflow workflow, Type activityType, ConstructorInfo constructor, MethodInfo method, byte[] args, LocalActivityOptions options)
            {
                return await (Task<byte[]>)executeLocalActivityAsync.Invoke(workflow, new object[] { activityType, constructor, method, args, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static ActivityOptions NormalizeOptions(CadenceClient client, ActivityOptions options, System.Type interfaceType)
            {
                return (ActivityOptions)activityOptionsNormalize.Invoke(null, new object[] { client, options, interfaceType });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static LocalActivityOptions NormalizeOptions(CadenceClient client, LocalActivityOptions options)
            {
                return (LocalActivityOptions)localActivityOptionsNormalize.Invoke(null, new object[] { client, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static ChildWorkflowOptions NormalizeOptions(CadenceClient client, ChildWorkflowOptions options, System.Type interfaceType)
            {
                return (ChildWorkflowOptions)childWorkflowOptionsNormalize.Invoke(null, new object[] { client, options, interfaceType });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static WorkflowOptions ToWorkflowOptions(ChildWorkflowOptions options)
            {
                return (WorkflowOptions)childWorkflowOptionsToWorkflowOptions.Invoke(options, Array.Empty<object>());
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static WorkflowOptions NormalizeOptions(CadenceClient client, WorkflowOptions options, System.Type interfaceType)
            {
                return (WorkflowOptions)workflowOptionsNormalize.Invoke(null, new object[] { client, options, interfaceType });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static WorkflowStub NewWorkflowStub(CadenceClient client, string workflowTypeName, WorkflowExecution execution, WorkflowOptions options)
            {
                return (WorkflowStub)workflowStubConstructor.Invoke(new object[] { client, workflowTypeName, execution, options });
            }
        }
");
        }

        /// <summary>
        /// Returns the <see cref="DynamicWorkflowStub"/> for a workflow interface, dynamically generating code
        /// to implement the stub if necessary.
        /// </summary>
        /// <param name="workflowInterface">The workflow interface type.</param>
        /// <param name="isChild">Indicates whether an external or child workflow stub is required.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <paramref name="workflowInterface"/>.</exception>
        public static DynamicWorkflowStub GetWorkflowStub(Type workflowInterface, bool isChild)
        {
            Covenant.Requires<ArgumentNullException>(workflowInterface != null, nameof(workflowInterface));

            var workflowAttribute     = workflowInterface.GetCustomAttribute<WorkflowAttribute>();
            var workflowInterfaceName = workflowInterface.FullName;

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            //-----------------------------------------------------------------
            // Check whether we already have generated a stub class for the interface.

            DynamicWorkflowStub stub;

            lock (syncLock)
            {
                if (isChild)
                {
                    if (workflowInterfaceToChildStub.TryGetValue(workflowInterface, out stub))
                    {
                        return stub;
                    }
                }
                else
                {
                    if (workflowInterfaceToStub.TryGetValue(workflowInterface, out stub))
                    {
                        return stub;
                    }
                }
            }

            //-----------------------------------------------------------------
            // We need to generate the stub class.

            // Scan the interface methods to identify those tagged as query, signal, 
            // or workflow methods and build a table that maps these method signatures
            // to the method type and the options specified by the attributes.
            //
            // We're also going to ensure that all interface methods are tagged
            // as being a signal, query, or workflow method and that no method
            // is tagged more than once and finally, that the interface has at
            // least one workflow method.
            //
            // Note this code will also ensure that all workflow interface methods
            // implement a task/async signature by returning a Task and also that
            // all signal and query methods have unique names.

            var methodSignatureToDetails = new Dictionary<string, WorkflowMethodDetails>();
            var signalNames              = new HashSet<string>();
            var queryTypes               = new HashSet<string>();

            foreach (var method in workflowInterface.GetMethods())
            {
                var details = new WorkflowMethodDetails() { Method = method };

                if (method.ReturnType.IsGenericType)
                {
                    details.ReturnType = method.ReturnType.GetGenericArguments().First();
                }
                else
                {
                    details.ReturnType = typeof(void);
                }

                var signalMethodAttributes = method.GetCustomAttributes<SignalMethodAttribute>().ToArray();
                var queryMethodAttributes  = method.GetCustomAttributes<QueryMethodAttribute>().ToArray();
                var workflowAttributes     = method.GetCustomAttributes<WorkflowMethodAttribute>().ToArray();
                var attributeCount         = signalMethodAttributes.Length + queryMethodAttributes.Length + workflowAttributes.Length;

                if (attributeCount == 0)
                {
                    throw new WorkflowTypeException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must have one of these attributes: [SignalMethod], [QueryMethod], or [WorkflowMethod]");
                }
                else if (attributeCount > 1)
                {
                    throw new WorkflowTypeException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] may only be tagged with one of these attributes: [SignalMethod], [QueryMethod], or [WorkflowMethod]");
                }

                if (signalMethodAttributes.Length > 0)
                {
                    var signalAttribute = signalMethodAttributes.First();

                    if (method.ReturnType.IsGenericType || method.ReturnType != typeof(Task))
                    {
                        throw new WorkflowTypeException($"Workflow signal method [{workflowInterface.FullName}.{method.Name}()] does not return a [Task].");
                    }

                    if (signalNames.Contains(signalAttribute.Name))
                    {
                        throw new WorkflowTypeException($"Workflow signal method [{workflowInterface.FullName}.{method.Name}()] specifies [SignalMethod(Name = {signalAttribute.Name})] which conflicts with another signal method.");
                    }

                    signalNames.Add(signalAttribute.Name);

                    details.Kind                  = WorkflowMethodKind.Signal;
                    details.SignalMethodAttribute = signalAttribute;
                }
                else if (queryMethodAttributes.Length > 0)
                {
                    var queryAttribute = queryMethodAttributes.First();

                    if (method.ReturnType != typeof(Task) && method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new WorkflowTypeException($"Workflow query method [{workflowInterface.FullName}.{method.Name}()] does not return a [Task].");
                    }

                    if (queryTypes.Contains(queryAttribute.Name))
                    {
                        throw new WorkflowTypeException($"Workflow query method [{workflowInterface.FullName}.{method.Name}()] specifies [QueryMethod(Name = {queryAttribute.Name})] which conflicts with another signal method.");
                    }

                    queryTypes.Add(queryAttribute.Name);

                    details.Kind                 = WorkflowMethodKind.Query;
                    details.QueryMethodAttribute = queryAttribute;
                }
                else if (workflowAttributes.Length > 0)
                {
                    var workflowMethodAttribute = workflowAttributes.First();

                    if (method.ReturnType != typeof(Task) && method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new WorkflowTypeException($"Workflow entry point method [{workflowInterface.FullName}.{method.Name}()] does not return a [Task].");
                    }

                    details.Kind                    = WorkflowMethodKind.Workflow;
                    details.WorkflowMethodAttribute = workflowMethodAttribute;
                }
                else
                {
                    Covenant.Assert(false); // We should never get here.
                }

                methodSignatureToDetails.Add(method.ToString(), details);
            }

            if (methodSignatureToDetails.Values.Count(d => d.Kind == WorkflowMethodKind.Workflow) == 0)
            {
                throw new WorkflowTypeException($"Workflow interface[{workflowInterface.FullName}] does not define a [WorkflowMethod].");
            }

            // Generate C# source code that implements the stub.  Note that stub classes will
            // be generated within the [Neon.Cadence.Stubs] namespace and will be named by
            // the interface name plus the "_Stub_#" (for external workflows) or "_ChildStub_#"
            // (for child workflows) suffix where "#" is the number of stubs generated so far.
            //
            // This will help avoid name conflicts while still being somewhat readable in 
            // debug stack traces

            var classId       = Interlocked.Increment(ref nextClassId);
            var stubClassName = isChild ? $"{workflowInterface.Name}_ChildStub_{classId}" : $"_{workflowInterface.Name}_Stub_{classId}";

            if (stubClassName.Length > 1 && stubClassName.StartsWith("I"))
            {
                stubClassName = stubClassName.Substring(1);
            }

            var stubFullClassName = $"Neon.Cadence.Stubs.{stubClassName}";
            var interfaceFullName = NormalizeTypeName(workflowInterface);
            var sbSource          = new StringBuilder();

            sbSource.AppendLine("#pragma warning disable CS0169  // Disable unreferenced field warnings.");
            sbSource.AppendLine("#pragma warning disable CS0649  // Disable unset field warnings.");
            sbSource.AppendLine();
            sbSource.AppendLine($"using System;");
            sbSource.AppendLine($"using System.Collections.Generic;");
            sbSource.AppendLine($"using System.ComponentModel;");
            sbSource.AppendLine($"using System.Diagnostics;");
            sbSource.AppendLine($"using System.Diagnostics.Contracts;");
            sbSource.AppendLine($"using System.Reflection;");
            sbSource.AppendLine($"using System.Runtime.CompilerServices;");
            sbSource.AppendLine($"using System.Threading.Tasks;");
            sbSource.AppendLine();
            sbSource.AppendLine($"using Neon.Cadence;");
            sbSource.AppendLine($"using Neon.Cadence.Internal;");
            sbSource.AppendLine($"using Neon.Common;");
            sbSource.AppendLine($"using Neon.Tasks;");
            sbSource.AppendLine();
            sbSource.AppendLine($"namespace Neon.Cadence.Stubs");
            sbSource.AppendLine($"{{");
            sbSource.AppendLine($"    public class {stubClassName} : ITypedWorkflowStub, {interfaceFullName}");
            sbSource.AppendLine($"    {{");

            sbSource.AppendLine($"        //-----------------------------------------------------------------");
            sbSource.AppendLine($"        // Private types");
            sbSource.AppendLine();

            AppendStubHelper(sbSource);    // Generate a private static [___StubHelper] class. 

            sbSource.AppendLine();
            sbSource.AppendLine($"        //-----------------------------------------------------------------");
            sbSource.AppendLine($"        // Implementation");
            sbSource.AppendLine();
            sbSource.AppendLine($"        private CadenceClient         client;");
            sbSource.AppendLine($"        private IDataConverter        dataConverter;");
            sbSource.AppendLine($"        private Workflow              parentWorkflow;");
            sbSource.AppendLine($"        private string                workflowTypeName;");
            sbSource.AppendLine($"        private System.Type           interfaceType;");
            sbSource.AppendLine($"        private bool                  isChild;");
            sbSource.AppendLine($"        private WorkflowOptions       options;");
            sbSource.AppendLine($"        private ChildWorkflowOptions  childOptions;");
            sbSource.AppendLine($"        private string                domain;");
            sbSource.AppendLine($"        private bool                  hasStarted;");
            sbSource.AppendLine($"        private WorkflowExecution     execution;");
            sbSource.AppendLine($"        private string                workflowId;");
            sbSource.AppendLine($"        private ChildExecution        childExecution;");
            sbSource.AppendLine($"        private bool                  continueAsNew;");
            sbSource.AppendLine($"        private ContinueAsNewOptions  continueAsNewOptions;");

            // Generate the constructor used for normal external workflow start stubs.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, string workflowTypeName, WorkflowOptions options, System.Type interfaceType = null)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client           = client;");
            sbSource.AppendLine($"            this.dataConverter    = dataConverter;");
            sbSource.AppendLine($"            this.workflowTypeName = workflowTypeName;");
            sbSource.AppendLine($"            this.options          = options;");
            sbSource.AppendLine($"            this.domain           = ___StubHelper.ResolveDomain(client, options.Domain);");
            sbSource.AppendLine($"            this.interfaceType    = interfaceType;");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to connect to an existing external workflow by IDs.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, string workflowId, string runId = null, string domain = null)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client        = client;");
            sbSource.AppendLine($"            this.dataConverter = dataConverter;");
            sbSource.AppendLine($"            this.hasStarted    = true;");
            sbSource.AppendLine($"            this.execution     = new WorkflowExecution(workflowId, runId);");
            sbSource.AppendLine($"            this.domain        = ___StubHelper.ResolveDomain(client, domain);");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to attach to an existing workflow by execution.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, WorkflowExecution execution, string domain)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client        = client;");
            sbSource.AppendLine($"            this.dataConverter = dataConverter;");
            sbSource.AppendLine($"            this.hasStarted    = true;");
            sbSource.AppendLine($"            this.execution     = execution;");
            sbSource.AppendLine($"            this.workflowId    = execution.WorkflowId;");
            sbSource.AppendLine($"            this.domain        = ___StubHelper.ResolveDomain(client, domain);");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used for child workflow stubs.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options, System.Type interfaceType = null)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client           = client;");
            sbSource.AppendLine($"            this.dataConverter    = dataConverter;");
            sbSource.AppendLine($"            this.parentWorkflow   = parentWorkflow;");
            sbSource.AppendLine($"            this.workflowTypeName = workflowTypeName;");
            sbSource.AppendLine($"            this.isChild          = true;");
            sbSource.AppendLine($"            this.childOptions     = options;");
            sbSource.AppendLine($"            this.domain           = this.childOptions.Domain;");
            sbSource.AppendLine($"            this.interfaceType    = interfaceType;");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used for already started child workflow stubs.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, Workflow parentWorkflow, string workflowTypeName, ChildExecution execution)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client           = client;");
            sbSource.AppendLine($"            this.dataConverter    = dataConverter;");
            sbSource.AppendLine($"            this.hasStarted       = true;");
            sbSource.AppendLine($"            this.parentWorkflow   = parentWorkflow;");
            sbSource.AppendLine($"            this.workflowTypeName = workflowTypeName;");
            sbSource.AppendLine($"            this.childExecution   = execution;");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to create a continue-as-new stub.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, string workflowTypeName, ContinueAsNewOptions options)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client               = client;");
            sbSource.AppendLine($"            this.dataConverter        = dataConverter;");
            sbSource.AppendLine($"            this.continueAsNew        = true;");
            sbSource.AppendLine($"            this.continueAsNewOptions = options ?? new ContinueAsNewOptions();");
            sbSource.AppendLine();
            sbSource.AppendLine($"            this.continueAsNewOptions.Workflow = workflowTypeName;");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to create an external child workflow stub by [WorkflowExecution].

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, Workflow parentWorkflow, WorkflowExecution execution)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client         = client;");
            sbSource.AppendLine($"            this.dataConverter  = dataConverter;");
            sbSource.AppendLine($"            this.parentWorkflow = parentWorkflow;");
            sbSource.AppendLine($"            this.hasStarted     = true;");
            sbSource.AppendLine($"            this.execution      = execution;");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to create an external child workflow stub by workflow ID and domain.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, Workflow parentWorkflow, string workflowId, string domain)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client         = client;");
            sbSource.AppendLine($"            this.dataConverter  = dataConverter;");
            sbSource.AppendLine($"            this.parentWorkflow = parentWorkflow;");
            sbSource.AppendLine($"            this.hasStarted     = true;");
            sbSource.AppendLine($"            this.workflowId     = workflowId;");
            sbSource.AppendLine($"            this.domain         = domain;");
            sbSource.AppendLine($"        }}");

            // Generate the method that converts the instance into a new untyped [WorkflowStub] (from ITypedWorkflowStub).

            sbSource.AppendLine();
            sbSource.AppendLine($"        public WorkflowStub ToUntyped()");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            if (isChild)");
            sbSource.AppendLine($"            {{");
            sbSource.AppendLine($"                if (!hasStarted || childExecution == null)");
            sbSource.AppendLine($"                {{");
            sbSource.AppendLine($"                    throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] has not been been started.\");");
            sbSource.AppendLine($"                }}");
            sbSource.AppendLine();

            // Note that we're converting the [ChildWorkflowOptions] into a [WorkflowOptions] 
            // as a bit of a hack to make the [WorkflowStub] constructor happy.

            sbSource.AppendLine($"                return (WorkflowStub)___StubHelper.NewWorkflowStub(client, workflowTypeName, childExecution.Execution, ___StubHelper.ToWorkflowOptions(childOptions));");
            sbSource.AppendLine($"            }}");
            sbSource.AppendLine($"            else");
            sbSource.AppendLine($"            {{");
            sbSource.AppendLine($"                if (!hasStarted || execution == null)");
            sbSource.AppendLine($"                {{");
            sbSource.AppendLine($"                    throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] has not been been started.\");");
            sbSource.AppendLine($"                }}");
            sbSource.AppendLine();
            sbSource.AppendLine($"                return (WorkflowStub)___StubHelper.NewWorkflowStub(client, workflowTypeName, execution, options);");
            sbSource.AppendLine($"            }}");
            sbSource.AppendLine($"        }}");

            // Generate the method that returns the external workflow stub's execution.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public WorkflowExecution GetExecution()");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            if (!hasStarted || (execution == null && childExecution == null))");
            sbSource.AppendLine($"            {{");
            sbSource.AppendLine($"                throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] has not been been started.\");");
            sbSource.AppendLine($"            }}");
            sbSource.AppendLine();
            sbSource.AppendLine($"            return execution ?? childExecution.Execution;");
            sbSource.AppendLine($"        }}");

            // Generate the workflow entry point methods.

            foreach (var details in methodSignatureToDetails.Values.Where(d => d.Kind == WorkflowMethodKind.Workflow))
            {
                var resultType = CadenceHelper.TypeToCSharp(details.ReturnType);
                var parameters = details.Method.GetParameters();
                var sbParams   = new StringBuilder();

                foreach (var param in parameters)
                {
                    sbParams.AppendWithSeparator($"{CadenceHelper.TypeToCSharp(param.ParameterType)} {param.Name}", ", ");
                }

                var resultTaskType = details.IsVoid ? "Task" : $"Task<{resultType}>";

                sbSource.AppendLine();
                sbSource.AppendLine($"        public async {resultTaskType} {details.Method.Name}({sbParams})");
                sbSource.AppendLine($"        {{");
                sbSource.AppendLine($"            await SyncContext.ClearAsync;");
                sbSource.AppendLine();
                sbSource.AppendLine($"            if (this.continueAsNew)");
                sbSource.AppendLine($"            {{");
                sbSource.AppendLine($"                var ___args = new List<object>();");

                if (parameters.Length > 0)
                {
                    sbSource.AppendLine();

                    foreach (var param in parameters)
                    {
                        sbSource.AppendLine($"                ___args.Add({param.Name});");
                    }
                }

                sbSource.AppendLine();
                sbSource.AppendLine($"                throw new ContinueAsNewException(this.dataConverter.ToData(___args), this.continueAsNewOptions);");
                sbSource.AppendLine($"            }}");
                sbSource.AppendLine();

                if (isChild)
                {
                    //---------------------------------------------------------
                    // Generate code for child workflows

                    sbSource.AppendLine($"            if (this.hasStarted)");
                    sbSource.AppendLine($"            {{");
                    sbSource.AppendLine($"                throw new WorkflowExecutionAlreadyStartedException(\"Workflow stub for [{workflowInterface.FullName}] has already been started.\");");
                    sbSource.AppendLine($"            }}");
                    sbSource.AppendLine();

                    if (string.IsNullOrEmpty(details.WorkflowMethodAttribute.Name))
                    {
                        sbSource.AppendLine($"            var ___workflowTypeName = this.workflowTypeName;");
                    }
                    else
                    {
                        sbSource.AppendLine($"            var ___workflowTypeName = $\"{{this.workflowTypeName}}::{details.WorkflowMethodAttribute.Name}\";");
                    }

                    sbSource.AppendLine($"            var ___options          = this.childOptions ?? new ChildWorkflowOptions();");

                    if (details.WorkflowMethodAttribute != null)
                    {
                        if (details.WorkflowMethodAttribute.WorkflowIdReusePolicy != WorkflowIdReusePolicy.UseDefault)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            ___options.WorkflowIdReusePolicy = {nameof(WorkflowIdReusePolicy)}.{details.WorkflowMethodAttribute.WorkflowIdReusePolicy};");
                        }

                        if (!string.IsNullOrEmpty(details.WorkflowMethodAttribute.TaskList))
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (string.IsNullOrEmpty(___options.TaskList))");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.TaskList = {StringLiteral(details.WorkflowMethodAttribute.TaskList)};");
                            sbSource.AppendLine($"            }}");
                        }

                        if (details.WorkflowMethodAttribute.ExecutionStartToCloseTimeoutSeconds > 0)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (___options.ExecutionStartToCloseTimeout <= TimeSpan.Zero)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.ExecutionStartToCloseTimeout = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.ExecutionStartToCloseTimeoutSeconds});");
                            sbSource.AppendLine($"            }}");
                        }

                        if (details.WorkflowMethodAttribute.ScheduleToStartTimeoutSeconds > 0)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (___options.ScheduleToStartTimeoutSeconds <= TimeSpan.Zero)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.ScheduleToStartTimeoutSeconds = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.ScheduleToStartTimeoutSeconds});");
                            sbSource.AppendLine($"            }}");
                        }

                        if (details.WorkflowMethodAttribute.TaskStartToCloseTimeoutSeconds > 0)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (___options.TaskStartToCloseTimeout <= TimeSpan.Zero)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.TaskStartToCloseTimeout = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.TaskList});");
                            sbSource.AppendLine($"            }}");
                        }

                        if (!string.IsNullOrEmpty(details.WorkflowMethodAttribute.WorkflowId))
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (string.IsNullOrEmpty(___options.WorkflowId)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.WorkflowId = {StringLiteral(details.WorkflowMethodAttribute.WorkflowId)};");
                            sbSource.AppendLine($"            }}");
                        }
                    }

                    sbSource.AppendLine();
                    sbSource.AppendLine($"            ___options = ___StubHelper.NormalizeOptions(this.client, this.childOptions, this.interfaceType);");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            byte[] ___argBytes    = {SerializeArgsExpression(details.Method.GetParameters())};");
                    sbSource.AppendLine($"            byte[] ___resultBytes = null;");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            this.hasStarted     = true;");
                    sbSource.AppendLine($"            this.childExecution = await ___StubHelper.StartChildWorkflowAsync(this.client, this.parentWorkflow, ___workflowTypeName, ___argBytes, ___options);");
                    sbSource.AppendLine($"            ___resultBytes      = await ___StubHelper.GetChildWorkflowResultAsync(this.client, this.parentWorkflow, this.childExecution);");
                    sbSource.AppendLine();

                    if (!details.IsVoid)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            return this.dataConverter.FromData<{resultType}>(___resultBytes);");
                    }
                }
                else
                {
                    //---------------------------------------------------------
                    // Generate code for external workflows

                    sbSource.AppendLine($"            if (this.hasStarted)");
                    sbSource.AppendLine($"            {{");
                    sbSource.AppendLine($"                throw new WorkflowExecutionAlreadyStartedException(\"Workflow stub for [{workflowInterface.FullName}] has already been started.\");");
                    sbSource.AppendLine($"            }}");
                    sbSource.AppendLine();

                    if (string.IsNullOrEmpty(details.WorkflowMethodAttribute.Name))
                    {
                        sbSource.AppendLine($"            var ___workflowTypeName = this.workflowTypeName;");
                    }
                    else
                    {
                        sbSource.AppendLine($"            var ___workflowTypeName = $\"{{this.workflowTypeName}}::{details.WorkflowMethodAttribute.Name}\";");
                    }

                    sbSource.AppendLine($"            var ___options          = this.options ?? new WorkflowOptions();");

                    if (details.WorkflowMethodAttribute != null)
                    {
                        if (details.WorkflowMethodAttribute.WorkflowIdReusePolicy != WorkflowIdReusePolicy.UseDefault)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            ___options.WorkflowIdReusePolicy = {nameof(WorkflowIdReusePolicy)}.{details.WorkflowMethodAttribute.WorkflowIdReusePolicy};");
                        }

                        if (!string.IsNullOrEmpty(details.WorkflowMethodAttribute.TaskList))
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (string.IsNullOrEmpty(___options.TaskList))");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.TaskList = {StringLiteral(details.WorkflowMethodAttribute.TaskList)};");
                            sbSource.AppendLine($"            }}");
                        }

                        if (details.WorkflowMethodAttribute.ExecutionStartToCloseTimeoutSeconds > 0)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (___options.ExecutionStartToCloseTimeout <= TimeSpan.Zero)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.ExecutionStartToCloseTimeout = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.ExecutionStartToCloseTimeoutSeconds});");
                            sbSource.AppendLine($"            }}");
                        }

                        if (details.WorkflowMethodAttribute.ScheduleToStartTimeoutSeconds > 0)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (___options.ScheduleToStartTimeoutSeconds <= TimeSpan.Zero)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.ScheduleToStartTimeoutSeconds = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.ScheduleToStartTimeoutSeconds});");
                            sbSource.AppendLine($"            }}");
                        }

                        if (details.WorkflowMethodAttribute.TaskStartToCloseTimeoutSeconds > 0)
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (___options.TaskStartToCloseTimeout <= TimeSpan.Zero)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.TaskStartToCloseTimeout = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.TaskList});");
                            sbSource.AppendLine($"            }}");
                        }

                        if (!string.IsNullOrEmpty(details.WorkflowMethodAttribute.WorkflowId))
                        {
                            sbSource.AppendLine();
                            sbSource.AppendLine($"            if (string.IsNullOrEmpty(___options.WorkflowId)");
                            sbSource.AppendLine($"            {{");
                            sbSource.AppendLine($"                ___options.WorkflowId = {StringLiteral(details.WorkflowMethodAttribute.WorkflowId)};");
                            sbSource.AppendLine($"            }}");
                        }
                    }

                    sbSource.AppendLine();
                    sbSource.AppendLine($"            ___options = ___StubHelper.NormalizeOptions(this.client, this.options, this.interfaceType);");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            byte[] ___argBytes    = {SerializeArgsExpression(details.Method.GetParameters())};");
                    sbSource.AppendLine($"            byte[] ___resultBytes = null;");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            this.hasStarted = true;");
                    sbSource.AppendLine($"            this.execution  = await ___StubHelper.StartWorkflowAsync(this.client, ___workflowTypeName, ___argBytes, ___options);");
                    sbSource.AppendLine($"            ___resultBytes  = await ___StubHelper.GetWorkflowResultAsync(this.client, this.execution, this.domain);");

                    if (!details.IsVoid)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            return this.dataConverter.FromData<{resultType}>(___resultBytes);");
                    }
                }

                sbSource.AppendLine($"        }}");
            }

            // Generate the workflow signal methods.  Note that these will vary a bit
            // between external and child workflows.

            foreach (var details in methodSignatureToDetails.Values.Where(d => d.Kind == WorkflowMethodKind.Signal))
            {
                var sbParams = new StringBuilder();
                
                foreach (var param in details.Method.GetParameters())
                {
                    sbParams.AppendWithSeparator($"{CadenceHelper.TypeToCSharp(param.ParameterType)} {param.Name}", ", ");
                }

                sbSource.AppendLine();
                sbSource.AppendLine($"        public async Task {details.Method.Name}({sbParams})");
                sbSource.AppendLine($"        {{");
                sbSource.AppendLine($"            await SyncContext.ClearAsync;");
                sbSource.AppendLine();

                if (isChild)
                {
                    // Code to signal a child workflow. 

                    sbSource.AppendLine($"            if (!this.hasStarted)");
                    sbSource.AppendLine($"            {{");
                    sbSource.AppendLine($"                throw new WorkflowExecutionAlreadyStartedException(\"Workflow stub for [{workflowInterface.FullName}] cannot be signalled because a workflow method needs to be called first.\");");
                    sbSource.AppendLine($"            }}");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            var ___argBytes = {SerializeArgsExpression(details.Method.GetParameters())};");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            await ___StubHelper.SignalChildWorkflowAsync(this.client, this.parentWorkflow, this.childExecution, {StringLiteral(details.SignalMethodAttribute.Name)}, ___argBytes);");
                }
                else
                {
                    // Code to signal an external workflow.

                    sbSource.AppendLine($"            if (!this.hasStarted)");
                    sbSource.AppendLine($"            {{");
                    sbSource.AppendLine($"                throw new WorkflowExecutionAlreadyStartedException(\"Workflow stub for [{workflowInterface.FullName}] cannot be signalled because a workflow method needs to be called first.\");");
                    sbSource.AppendLine($"            }}");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            var ___argBytes = {SerializeArgsExpression(details.Method.GetParameters())};");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            await ___StubHelper.SignalWorkflowAsync(this.client, this.execution, {StringLiteral(details.SignalMethodAttribute.Name)}, ___argBytes, this.domain);");
                }

                sbSource.AppendLine($"        }}");
            }

            // Generate the workflow query methods.

            foreach (var details in methodSignatureToDetails.Values.Where(d => d.Kind == WorkflowMethodKind.Query))
            {
                var resultType = CadenceHelper.TypeToCSharp(details.ReturnType);
                var sbParams   = new StringBuilder();

                foreach (var param in details.Method.GetParameters())
                {
                    sbParams.AppendWithSeparator($"{CadenceHelper.TypeToCSharp(param.ParameterType)} {param.Name}", ", ");
                }

                var resultTaskType = details.IsVoid ? "Task" : $"Task<{resultType}>";

                sbSource.AppendLine();
                sbSource.AppendLine($"        public async {resultTaskType} {details.Method.Name}({sbParams})");
                sbSource.AppendLine($"        {{");
                sbSource.AppendLine($"            await SyncContext.ClearAsync;");
                sbSource.AppendLine();

                if (isChild)
                {
                    // Querying child workflows is not currently supported by Cadence.

                    sbSource.AppendLine($"            throw new NotSupportedException(\"Workflow stub for [{workflowInterface.FullName}] cannot be queried because it was created for a child workflow.  Querying child workflows is not supported by Cadence.\");");
                }
                else
                {
                    sbSource.AppendLine($"            if (this.execution == null)");
                    sbSource.AppendLine($"            {{");
                    sbSource.AppendLine($"                throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] cannot be queried because a workflow method needs to be called first.\");");
                    sbSource.AppendLine($"            }}");
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            var ___argBytes    = {SerializeArgsExpression(details.Method.GetParameters())};");
                    sbSource.AppendLine($"            var ___resultBytes = await ___StubHelper.QueryWorkflowAsync(this.client, this.execution, {StringLiteral(details.QueryMethodAttribute.Name)}, ___argBytes, this.domain);");

                    if (!details.IsVoid)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            return this.dataConverter.FromData<{resultType}>(___resultBytes);");
                    }
                }

                sbSource.AppendLine($"        }}");
            }

            // Close out the stub class and namespace.

            sbSource.AppendLine($"    }}");
            sbSource.AppendLine($"}}");

            var source = sbSource.ToString();

            //-----------------------------------------------------------------
            // Compile the new workflow stub class into an assembly.

            //------------------------------
            // $todo(jefflill): DELETE THIS!
            var interfaceName = workflowInterface.Name;
            //------------------------------

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var dotnetPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var references = new List<MetadataReference>();

            // Reference these required assemblies.

            references.Add(MetadataReference.CreateFromFile(typeof(NeonHelper).Assembly.Location));

            // Reference all loaded assemblies.

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            var assemblyName    = $"Neon-Cadence-WorkflowStub-{classId}";
            var compilerOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);
            var compilation     = CSharpCompilation.Create(assemblyName, new[] { syntaxTree }, references, compilerOptions);
            var assemblyStream  = new MemoryStream();

            using (var pdbStream = new MemoryStream())
            {
                var emitted = compilation.Emit(assemblyStream, pdbStream);

                if (!emitted.Success)
                {
                    throw new StubCompilerException(emitted.Diagnostics);
                }
            }

            assemblyStream.Position = 0;

            //-----------------------------------------------------------------
            // Load the new assembly into the current context.  Note that we're
            // going to do this within a lock because it's possible that we created
            // two stubs for the same workflow interface in parallel and we need
            // to ensure that we're only going to load one of them.

            lock (syncLock)
            {
                if (isChild)
                {
                    if (!workflowInterfaceToChildStub.TryGetValue(workflowInterface, out stub))
                    {
                        var stubAssembly = CadenceHelper.LoadAssembly(assemblyStream);
                        var stubType     = stubAssembly.GetType(stubFullClassName);

                        stub = new DynamicWorkflowStub(stubType, stubAssembly, stubFullClassName);

                        workflowInterfaceToChildStub.Add(workflowInterface, stub);
                    }
                }
                else
                {
                    if (!workflowInterfaceToStub.TryGetValue(workflowInterface, out stub))
                    {
                        var stubAssembly = CadenceHelper.LoadAssembly(assemblyStream);
                        var stubType     = stubAssembly.GetType(stubFullClassName);

                        stub = new DynamicWorkflowStub(stubType, stubAssembly, stubFullClassName);

                        workflowInterfaceToStub.Add(workflowInterface, stub);
                    }
                }
            }

            return stub;
        }

        /// <summary>
        /// Creates a dynamically generated stub to be used to connect to an existing an external workflow.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow's run ID.</param>
        /// <param name="domain">Optionally specifies a domain that </param>
        /// <returns>The stub instance.</returns>
        public static TWorkflowInterface NewWorkflowStub<TWorkflowInterface>(CadenceClient client, string workflowId, string runId = null, string domain = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            var stub = GetWorkflowStub(typeof(TWorkflowInterface), isChild: false);

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, workflowId, runId, domain);
        }

        /// <summary>
        /// Creates a dynamically generated stub to be used to create an external workflow with the
        /// specified workflow interface.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="workflowTypeName">Optionally specifies the workflow type name.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <typeparamref name="TWorkflowInterface"/>.</exception>
        public static TWorkflowInterface NewWorkflowStub<TWorkflowInterface>(CadenceClient client, WorkflowOptions options = null, string workflowTypeName = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            options = WorkflowOptions.Normalize(client, options, typeof(TWorkflowInterface));

            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowInterface, workflowAttribute);
            }

            var stub = GetWorkflowStub(typeof(TWorkflowInterface), isChild: false);

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, workflowTypeName, options, typeof(TWorkflowInterface));
        }

        /// <summary>
        /// Creates a dynamically generated stub for the specified child workflow interface.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="workflowTypeName">Optionally specifies the workflow type name.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <typeparamref name="TWorkflowInterface"/>.</exception>
        public static TWorkflowInterface NewChildWorkflowStub<TWorkflowInterface>(CadenceClient client, Workflow parentWorkflow, ChildWorkflowOptions options = null, string workflowTypeName = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            options = ChildWorkflowOptions.Normalize(client, options, typeof(TWorkflowInterface));

            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowInterface, workflowAttribute);
            }

            var stub = GetWorkflowStub(typeof(TWorkflowInterface), isChild: true);

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, parentWorkflow, workflowTypeName, options, typeof(TWorkflowInterface));
        }

        /// <summary>
        /// Creates a dynamically generated stub for the specified child workflow interface.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">Optionally specifies the workflow type name.</param>
        /// <param name="execution">The child execution.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <typeparamref name="TWorkflowInterface"/>.</exception>
        public static TWorkflowInterface NewChildWorkflowStub<TWorkflowInterface>(CadenceClient client, Workflow parentWorkflow, string workflowTypeName, ChildExecution execution)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowInterface, workflowAttribute);
            }

            var stub = GetWorkflowStub(typeof(TWorkflowInterface), isChild: true);

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, parentWorkflow, workflowTypeName, execution);
        }

        /// <summary>
        /// Creates a dynamically generated external stub for an existing child workflow using the
        /// workflow ID and optional domain.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowId">The child workflow ID.</param>
        /// <param name="domain">Optionally overrides the parent workflow domain.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <typeparamref name="TWorkflowInterface"/>.</exception>
        public static TWorkflowInterface NewChildWorkflowStubById<TWorkflowInterface>(CadenceClient client, Workflow parentWorkflow, string workflowId, string domain = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId), nameof(workflowId));

            domain = domain ?? parentWorkflow.WorkflowInfo.Domain;

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            var stub = GetWorkflowStub(typeof(TWorkflowInterface), isChild: true);

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, parentWorkflow, workflowId, domain);
        }

        /// <summary>
        /// Creates a dynamically generated external stub for an existing child workflow using the
        /// workflow execution.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="execution">The child's external workflow execution.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <typeparamref name="TWorkflowInterface"/>.</exception>
        public static TWorkflowInterface NewChildWorkflowStubById<TWorkflowInterface>(CadenceClient client, Workflow parentWorkflow, WorkflowExecution execution)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            var stub = GetWorkflowStub(typeof(TWorkflowInterface), isChild: true);

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, parentWorkflow, execution);
        }

        /// <summary>
        /// Creates a dynamically generated stub that when called will continue the workflow as new.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="options">Optionally continuation options.</param>
        /// <returns>The stub instance.</returns>
        /// <exception cref="WorkflowTypeException">Thrown when there are problems with the <typeparamref name="TWorkflowInterface"/>.</exception>
        public static TWorkflowInterface NewContinueAsNewStub<TWorkflowInterface>(CadenceClient client, ContinueAsNewOptions options = null)
            where TWorkflowInterface : class
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var workflowInterface = typeof(TWorkflowInterface);
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            var stub             = GetWorkflowStub(typeof(TWorkflowInterface), isChild: false);
            var workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowInterface, workflowInterface.GetCustomAttribute<WorkflowAttribute>());

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, workflowTypeName, options);
        }

        /// <summary>
        /// Returns a <see cref="DynamicActivityStub"/> wrapping the specified activity interface for
        /// a workflow, dynamically generating the required type if required.
        /// </summary>
        /// <param name="activityInterface">The activity interface type.</param>
        /// <returns>The activity stub instance.</returns>
        /// <exception cref="ActivityTypeException">Thrown when there are problems with the <paramref name="activityInterface"/>.</exception>
        public static DynamicActivityStub GetActivityStub(Type activityInterface)
        {
            Covenant.Requires<ArgumentNullException>(activityInterface != null, nameof(activityInterface));

            var activityTypeName  = activityInterface.FullName;
            var activityAttribute = activityInterface.GetCustomAttribute<ActivityAttribute>();

            //-----------------------------------------------------------------
            // Check whether we already have generated a stub class for the interface
            // and return the stub instance right away.

            DynamicActivityStub stub;

            lock (syncLock)
            {
                if (activityInterfaceToStub.TryGetValue(activityInterface, out stub))
                {
                    return stub;
                }
            }

            //-----------------------------------------
            // $todo(jefflill): DELETE THIS!
            var interfaceName = activityInterface.Name;
            //-----------------------------------------

            //-----------------------------------------------------------------
            // We need to generate the stub class.

            // Scan the interface methods to ensure that all of them implement a 
            // task/async signature by returning a Task.  We're also going to 
            // build a table that maps the method signature to additional details.

            var methodSignatureToDetails = new Dictionary<string, ActivityMethodDetails>();

            foreach (var method in activityInterface.GetMethods())
            {
                var details = new ActivityMethodDetails()
                {
                    ActivityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>() ?? new ActivityMethodAttribute() { Name = string.Empty },
                    Method                  = method
                };

                if (method.ReturnType.IsGenericType)
                {
                    details.ReturnType = method.ReturnType.GetGenericArguments().First();
                }
                else
                {
                    details.ReturnType = typeof(void);
                }

                methodSignatureToDetails.Add(method.ToString(), details);
            }

            // Generate C# source code that implements the stub.  Note that stub classes will
            // be generated within the [Neon.Cadence.Stubs] namespace and will be named by
            // the interface name plus the "_Stub_#" suffix where "#" is the number of stubs
            // generated so far.  This will help avoid naming conflicts while still being
            // somewhat readable in debug stack traces.

            var classId       = Interlocked.Increment(ref nextClassId);
            var stubClassName = $"{activityInterface.Name}_Stub_{classId}";

            if (stubClassName.Length > 1 && stubClassName.StartsWith("I"))
            {
                stubClassName = stubClassName.Substring(1);
            }

            var stubFullClassName = $"Neon.Cadence.Stubs.{stubClassName}";
            var interfaceFullName = NormalizeTypeName(activityInterface);
            var sbSource          = new StringBuilder();

            sbSource.AppendLine($"using System;");
            sbSource.AppendLine($"using System.Collections.Generic;");
            sbSource.AppendLine($"using System.ComponentModel;");
            sbSource.AppendLine($"using System.Diagnostics;");
            sbSource.AppendLine($"using System.Diagnostics.Contracts;");
            sbSource.AppendLine($"using System.Reflection;");
            sbSource.AppendLine($"using System.Runtime.CompilerServices;");
            sbSource.AppendLine($"using System.Threading.Tasks;");
            sbSource.AppendLine();
            sbSource.AppendLine($"using Neon.Cadence;");
            sbSource.AppendLine($"using Neon.Cadence.Internal;");
            sbSource.AppendLine($"using Neon.Common;");
            sbSource.AppendLine($"using Neon.Tasks;");
            sbSource.AppendLine();
            sbSource.AppendLine($"namespace Neon.Cadence.Stubs");
            sbSource.AppendLine($"{{");
            sbSource.AppendLine($"    public class {stubClassName} : {interfaceFullName}");
            sbSource.AppendLine($"    {{");
            sbSource.AppendLine($"        //-----------------------------------------------------------------");
            sbSource.AppendLine($"        // Private types");
            sbSource.AppendLine();
            AppendStubHelper(sbSource);    // Generate a private static [___StubHelper] class. 

            sbSource.AppendLine();
            sbSource.AppendLine($"        //-----------------------------------------------------------------");
            sbSource.AppendLine($"        // Implementation");
            sbSource.AppendLine();
            sbSource.AppendLine($"        private CadenceClient                     client;");
            sbSource.AppendLine($"        private IDataConverter                    dataConverter;");
            sbSource.AppendLine($"        private Workflow                          workflow;");
            sbSource.AppendLine($"        private bool                              isLocal;");
            sbSource.AppendLine($"        private string                            activityTypeName;");
            sbSource.AppendLine($"        private ActivityOptions                   options;");
            sbSource.AppendLine($"        private string                            domain;");
            sbSource.AppendLine($"        private Type                              activityInterface;");
            sbSource.AppendLine($"        private Type                              activityType;");
            sbSource.AppendLine($"        private ConstructorInfo                   activityConstructor;");
            sbSource.AppendLine($"        private LocalActivityOptions              localOptions;");
            sbSource.AppendLine($"        private Dictionary<string, MethodInfo>    nameToMethod;");

            // Generate the constructor for regular (non-local) activity stubs.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, Workflow workflow, string activityTypeName, ActivityOptions options, System.Type interfaceType)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client            = client;");
            sbSource.AppendLine($"            this.dataConverter     = dataConverter;");
            sbSource.AppendLine($"            this.workflow          = workflow;");
            sbSource.AppendLine($"            this.isLocal           = false;");
            sbSource.AppendLine($"            this.activityInterface = interfaceType;");
            sbSource.AppendLine($"            this.activityTypeName  = activityTypeName;");
            sbSource.AppendLine($"            this.options           = ___StubHelper.NormalizeOptions(client, options, interfaceType);");
            sbSource.AppendLine($"            this.domain            = options.Domain;");
            sbSource.AppendLine($"        }}");

            // Generate the constructor for local activity stubs.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, Workflow workflow, Type activityType, LocalActivityOptions localOptions, System.Type interfaceType)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client              = client;");
            sbSource.AppendLine($"            this.dataConverter       = dataConverter;");
            sbSource.AppendLine($"            this.workflow            = workflow;");
            sbSource.AppendLine($"            this.isLocal             = true;");
            sbSource.AppendLine($"            this.activityInterface   = interfaceType;");
            sbSource.AppendLine($"            this.activityType        = activityType;");
            sbSource.AppendLine($"            this.activityConstructor = activityType.GetConstructor(Type.EmptyTypes);");
            sbSource.AppendLine($"            this.localOptions        = ___StubHelper.NormalizeOptions(client, localOptions);");
            sbSource.AppendLine();
            sbSource.AppendLine($"            if (this.activityConstructor == null)");
            sbSource.AppendLine($"            {{");
            sbSource.AppendLine($"                throw new ArgumentException($\"Activity type [{{activityType.FullName}}] does not have a public default constructor.\", nameof(activityType));");
            sbSource.AppendLine($"            }}");
            sbSource.AppendLine();
            sbSource.AppendLine($"            this.nameToMethod = new Dictionary<string, MethodInfo>();");
            sbSource.AppendLine();
            sbSource.AppendLine($"            foreach (var ___method in activityInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance))");
            sbSource.AppendLine($"            {{");
            sbSource.AppendLine($"                var ___activityMethodAttribute = ___method.GetCustomAttribute<ActivityMethodAttribute>();");
            sbSource.AppendLine();
            sbSource.AppendLine($"                if (___activityMethodAttribute == null)");
            sbSource.AppendLine($"                {{");
            sbSource.AppendLine($"                    continue;");
            sbSource.AppendLine($"                }}");
            sbSource.AppendLine();
            sbSource.AppendLine($"                this.nameToMethod.Add(___activityMethodAttribute.Name ?? string.Empty, ___method);");
            sbSource.AppendLine($"            }}");
            sbSource.AppendLine($"        }}");

            // Generate the activity methods.

            foreach (var details in methodSignatureToDetails.Values)
            {
                var resultType = CadenceHelper.TypeToCSharp(details.ReturnType);
                var sbParams   = new StringBuilder();

                foreach (var param in details.Method.GetParameters())
                {
                    sbParams.AppendWithSeparator($"{CadenceHelper.TypeToCSharp(param.ParameterType)} {param.Name}", ", ");
                }

                var resultTaskType = details.IsVoid ? "Task" : $"Task<{resultType}>";

                sbSource.AppendLine();
                sbSource.AppendLine($"        public async {resultTaskType} {details.Method.Name}({sbParams})");
                sbSource.AppendLine($"        {{");
                sbSource.AppendLine($"            await SyncContext.ClearAsync;");
                sbSource.AppendLine();
                sbSource.AppendLine($"            byte[]    ___argBytes = {SerializeArgsExpression(details.Method.GetParameters())};");
                sbSource.AppendLine($"            byte[]    ___resultBytes;");
                sbSource.AppendLine();
                sbSource.AppendLine($"            if (!isLocal)");
                sbSource.AppendLine($"            {{");
                sbSource.AppendLine($"                // Configure the regular activity call.");
                sbSource.AppendLine();

                if (string.IsNullOrEmpty(details.ActivityMethodAttribute.Name))
                {
                    sbSource.AppendLine($"                var ___activityTypeName = this.activityTypeName;");
                }
                else
                {
                    sbSource.AppendLine($"                var ___activityTypeName = $\"{{this.activityTypeName}}::{details.ActivityMethodAttribute.Name}\";");
                }

                sbSource.AppendLine($"                var ___options          = this.options.Clone();");

                if (details.ActivityMethodAttribute != null)
                {
                    if (!string.IsNullOrEmpty(details.ActivityMethodAttribute.TaskList))
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"                if (string.IsNullOrEmpty(___options.TaskList))");
                        sbSource.AppendLine($"                {{");
                        sbSource.AppendLine($"                    ___taskList = {StringLiteral(details.ActivityMethodAttribute.TaskList)};");
                        sbSource.AppendLine($"                }}");
                    }

                    if (details.ActivityMethodAttribute.HeartbeatTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"                if (___options.HeartbeatTimeout <= TimeSpan.Zero)");
                        sbSource.AppendLine($"                {{");
                        sbSource.AppendLine($"                    ___options.HeartbeatTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.HeartbeatTimeoutSeconds});");
                        sbSource.AppendLine($"                }}");
                    }

                    if (details.ActivityMethodAttribute.ScheduleToCloseTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"                if (___options.ScheduleToCloseTimeout <= TimeSpan.Zero)");
                        sbSource.AppendLine($"                {{");
                        sbSource.AppendLine($"                    ___options.ScheduleToCloseTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.ScheduleToCloseTimeoutSeconds});");
                        sbSource.AppendLine($"                }}");
                    }

                    if (details.ActivityMethodAttribute.ScheduleToStartTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"                if (___options.ScheduleToStartTimeout <= TimeSpan.Zero)");
                        sbSource.AppendLine($"                {{");
                        sbSource.AppendLine($"                    ___options.ScheduleToStartTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.ScheduleToStartTimeoutSeconds});");
                        sbSource.AppendLine($"                }}");
                    }

                    if (details.ActivityMethodAttribute.StartToCloseTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"                if (___options.StartToCloseTimeout <= TimeSpan.Zero)");
                        sbSource.AppendLine($"                {{");
                        sbSource.AppendLine($"                    ___options.StartToCloseTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.StartToCloseTimeoutSeconds});");
                        sbSource.AppendLine($"                }}");
                    }
                }

                sbSource.AppendLine();
                sbSource.AppendLine($"                // Execute the activity.");
                sbSource.AppendLine();
                sbSource.AppendLine($"                ___resultBytes = await ___StubHelper.ExecuteActivityAsync(this.workflow, ___activityTypeName, ___argBytes, this.options);");
                sbSource.AppendLine($"            }}");
                sbSource.AppendLine($"            else");
                sbSource.AppendLine($"            {{");
                sbSource.AppendLine($"                // Configure the local activity options.");
                sbSource.AppendLine();
                sbSource.AppendLine($"                var ___localOptions = this.localOptions.Clone();");

                if (details.ActivityMethodAttribute.StartToCloseTimeoutSeconds > 0)
                {
                    sbSource.AppendLine();
                    sbSource.AppendLine($"                if (___localOptions.ScheduleToCloseTimeout <= TimeSpan.Zero && details.ActivityMethodAttribute.StartToCloseTimeoutSeconds > 0 <= TimeSpan.Zero)");
                    sbSource.AppendLine($"                {{");
                    sbSource.AppendLine($"                    ___localOptions.ScheduleToCloseTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.ScheduleToCloseTimeoutSeconds});");
                    sbSource.AppendLine($"                }}");
                }

                sbSource.AppendLine();
                sbSource.AppendLine($"                // Execute the local activity.");
                sbSource.AppendLine();
                sbSource.AppendLine($"                if (!this.nameToMethod.TryGetValue(\"{details.ActivityMethodAttribute.Name ?? string.Empty}\", out var ___activityMethod))");
                sbSource.AppendLine($"                {{");
                sbSource.AppendLine($"                    throw new ArgumentException($\"Activity type [{{activityType.FullName}}] does not have an activity method named [\\\"{details.ActivityMethodAttribute.Name ?? string.Empty}\\\"].  Be sure your activity method is tagged by [ActivityMethod].\", nameof(activityType));");
                sbSource.AppendLine($"                }}");
                sbSource.AppendLine();
                sbSource.AppendLine($"                ___resultBytes = await ___StubHelper.ExecuteLocalActivityAsync(this.workflow, this.activityType, this.activityConstructor, ___activityMethod, ___argBytes, ___localOptions);");
                sbSource.AppendLine($"            }}");

                if (!details.IsVoid)
                {
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            return this.dataConverter.FromData<{resultType}>(___resultBytes);");
                }

                sbSource.AppendLine($"        }}");
            }

            // Close out the stub class and namespace.

            sbSource.AppendLine($"    }}");
            sbSource.AppendLine($"}}");

            var source = sbSource.ToString();

            //-----------------------------------------------------------------
            // Compile the new activity stub class into an assembly.

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var dotnetPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var references = new List<MetadataReference>();

            // Reference these required assemblies.

            references.Add(MetadataReference.CreateFromFile(typeof(NeonHelper).Assembly.Location));

            // Reference all loaded assemblies.

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            var assemblyName    = $"Neon-Cadence-WorkflowStub-{classId}";
            var compilerOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);
            var compilation     = CSharpCompilation.Create(assemblyName, new[] { syntaxTree }, references, compilerOptions);
            var dllStream       = new MemoryStream();

            using (var pdbStream = new MemoryStream())
            {
                var emitted = compilation.Emit(dllStream, pdbStream);

                if (!emitted.Success)
                {
                    throw new StubCompilerException(emitted.Diagnostics);
                }
            }

            dllStream.Position = 0;

            //-----------------------------------------------------------------
            // Load the new assembly into the current context.  Note that we're
            // going to do this within a lock because it's possible that we created
            // two stubs for the same activity interface in parallel and we need
            // to ensure that we're only going to load one of them.

            lock (syncLock)
            {
                if (!activityInterfaceToStub.TryGetValue(activityInterface, out stub))
                {
                    var stubAssembly = CadenceHelper.LoadAssembly(dllStream);
                    var stubType     = stubAssembly.GetType(stubFullClassName);

                    stub = new DynamicActivityStub(stubType, stubAssembly, stubFullClassName);

                    activityInterfaceToStub.Add(activityInterface, stub);
                }
            }

            return stub;
        }

        /// <summary>
        /// Creates a dynamically generated normal (non-local) activity stub for the specified activity interface.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="workflow">The parent workflow.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The activity stub instance.</returns>
        /// <exception cref="ActivityTypeException">Thrown when there are problems with the <typeparamref name="TActivityInterface"/>.</exception>
        public static TActivityInterface NewActivityStub<TActivityInterface>(CadenceClient client, Workflow workflow, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(workflow != null, nameof(workflow));

            var activityInterface = typeof(TActivityInterface);
            var activityAttribute = activityInterface.GetCustomAttribute<ActivityAttribute>();

            CadenceHelper.ValidateActivityInterface(activityInterface);

            var activityTypeName = CadenceHelper.GetActivityTypeName(activityInterface, activityAttribute);

            options = ActivityOptions.Normalize(client, options, typeof(TActivityInterface));

            var stub = GetActivityStub(typeof(TActivityInterface));

            return (TActivityInterface)stub.Create(client, workflow, activityTypeName, options, typeof(TActivityInterface));
        }

        /// <summary>
        /// Creates a dynamically generated local activity stub for the specified activity interface.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface type.</typeparam>
        /// <typeparam name="TActivityImplementation">The activity implementation.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="workflow">The parent workflow.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The activity stub instance.</returns>
        /// <exception cref="ActivityTypeException">Thrown when there are problems with the <typeparamref name="TActivityInterface"/>.</exception>
        public static TActivityInterface NewLocalActivityStub<TActivityInterface, TActivityImplementation>(CadenceClient client, Workflow workflow, LocalActivityOptions options = null)
            where TActivityImplementation : TActivityInterface
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(workflow != null, nameof(workflow));

            var activityType = typeof(TActivityImplementation);

            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            CadenceHelper.ValidateActivityImplementation(activityType);

            var stub = GetActivityStub(typeof(TActivityInterface));

            return (TActivityInterface)stub.CreateLocal(client, workflow, activityType, options ?? new LocalActivityOptions());
        }

        /// <summary>
        /// Returns the C# expression that uses the stub's data converter to
        /// serialize workflow method parameters to a byte array.
        /// </summary>
        /// <param name="args">The parameters.</param>
        /// <returns>The C# expression.</returns>
        private static string SerializeArgsExpression(ParameterInfo[] args)
        {
            var sb = new StringBuilder();

            foreach (var arg in args)
            {
                sb.AppendWithSeparator(arg.Name, ", ");
            }

            return $"this.dataConverter.ToData(new object[] {{ {sb} }})";
        }

        /// <summary>
        /// Renders the string passed as a C# literal, escaping any double quotes.
        /// </summary>
        public static string StringLiteral(string value)
        {
            if (value == null)
            {
                return "null";
            }

            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
