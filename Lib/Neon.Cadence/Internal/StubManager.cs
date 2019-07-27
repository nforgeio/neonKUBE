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

        // This dictionary maps workflow interfaces to their dynamically generated stubs.

        private static Dictionary<Type, DynamicWorkflowStub> workflowInterfaceToStub = new Dictionary<Type, DynamicWorkflowStub>();

        // ...and this one does the same for activities.

        private static Dictionary<Type, DynamicActivityStub> activityInterfaceToStub = new Dictionary<Type, DynamicActivityStub>();

        /// <summary>
        /// Creates a dynamically generated stub for the specified workflow interface.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="taskList">Optionally specifies the target task list.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="domain">Optionally specifies the target domain.</param>
        /// <returns>The stub instance.</returns>
        public static TWorkflowInterface CreateWorkflowStub<TWorkflowInterface>(CadenceClient client, string taskList = null, WorkflowOptions options = null, string domain = null)
            where TWorkflowInterface : class, IWorkflowBase
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            var workflowInterface = typeof(TWorkflowInterface);

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            options = options ?? new WorkflowOptions();

            if (string.IsNullOrEmpty(domain))
            {
                domain = null;
            }

            var workflowTypeName  = workflowInterface.FullName;
            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();

            //-----------------------------------------------------------------
            // Check whether we already have generated a stub class for the interface
            // and return a stub instance right away.

            DynamicWorkflowStub stub;

            lock (syncLock)
            {
                if (!workflowInterfaceToStub.TryGetValue(workflowInterface, out stub))
                {
                    stub = null;
                }
            }

            if (stub != null)
            {
                return (TWorkflowInterface)stub.Create(client, client.DataConverter, workflowTypeName, taskList, options, domain);
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
                    if (method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new WorkflowDefinitionException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must return a Task.");
                    }

                    details.ReturnType = method.ReturnType.GetGenericArguments().First();
                }
                else
                {
                    if (method.ReturnType != typeof(Task))
                    {
                        throw new WorkflowDefinitionException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must return a Task.");
                    }

                    details.ReturnType = typeof(void);
                }

                var signalMethodAttributes = method.GetCustomAttributes<SignalMethodAttribute>().ToArray();
                var queryMethodAttributes  = method.GetCustomAttributes<QueryMethodAttribute>().ToArray();
                var workflowAttributes     = method.GetCustomAttributes<WorkflowMethodAttribute>().ToArray();
                var attributeCount         = signalMethodAttributes.Length + queryMethodAttributes.Length + workflowAttributes.Length;

                if (attributeCount == 0)
                {
                    throw new WorkflowDefinitionException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must have one of these attributes: [SignalMethod], [QueryMethod], or [WorkflowMethod]");
                }
                else if (attributeCount > 1)
                {
                    throw new WorkflowDefinitionException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] may only be tagged with one of these attributes: [SignalMethod], [QueryMethod], or [WorkflowMethod]");
                }

                if (signalMethodAttributes.Length > 0)
                {
                    var signalAttribute = signalMethodAttributes.First();

                    if (method.ReturnType.IsGenericType || method.ReturnType != typeof(Task))
                    {
                        throw new WorkflowDefinitionException($"Workflow signal method [{workflowInterface.FullName}.{method.Name}()] does not return a [Task].");
                    }

                    if (signalNames.Contains(signalAttribute.Name))
                    {
                        throw new WorkflowDefinitionException($"Workflow signal method [{workflowInterface.FullName}.{method.Name}()] specifies [SignalMethod(Name = {signalAttribute.Name})] which conflicts with another signal method.");
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
                        throw new WorkflowDefinitionException($"Workflow query method [{workflowInterface.FullName}.{method.Name}()] does not return a [Task].");
                    }

                    if (queryTypes.Contains(queryAttribute.Name))
                    {
                        throw new WorkflowDefinitionException($"Workflow query method [{workflowInterface.FullName}.{method.Name}()] specifies [QueryMethod(Name = {queryAttribute.Name})] which conflicts with another signal method.");
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
                        throw new WorkflowDefinitionException($"Workflow entry point method [{workflowInterface.FullName}.{method.Name}()] does not return a [Task].");
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
                throw new WorkflowDefinitionException($"Workflow interface[{workflowInterface.FullName}] does not define a [WorkflowMethod].");
            }

            // Generate C# source code that implements the stub.  Note that stub classes will
            // be generated within the [Neon.Cadence.Stubs] namespace and will be named by
            // the interface name plus the "Stub_#" suffix where "#" is the number of stubs
            // generated so far.  This will help avoid naming conflicts while still being
            // somewhat readable in debug stack traces

            var classId       = Interlocked.Increment(ref nextClassId);
            var stubClassName = $"{workflowInterface.Name}Stub_{classId}";

            if (stubClassName.Length > 1 && stubClassName.StartsWith("I"))
            {
                stubClassName = stubClassName.Substring(1);
            }

            var stubFullClassName = $"Neon.Cadence.Stubs.{stubClassName}";
            var interfaceFullName = workflowInterface.FullName.Replace('+', '.');   // .NET uses "+" for nested types; convert these to "."
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
            sbSource.AppendLine($"using Neon.Common;");
            sbSource.AppendLine($"using Neon.Cadence;");
            sbSource.AppendLine();
            sbSource.AppendLine($"namespace Neon.Cadence.Stubs");
            sbSource.AppendLine($"{{");
            sbSource.AppendLine($"    public class {stubClassName} : {interfaceFullName}");
            sbSource.AppendLine($"    {{");

            AppendStubHelper(sbSource);    // Generate a private static [___StubHelper] class. 

            sbSource.AppendLine();
            sbSource.AppendLine($"        private CadenceClient         client;");
            sbSource.AppendLine($"        private IDataConverter        dataConverter;");
            sbSource.AppendLine($"        private string                workflowTypeName;");
            sbSource.AppendLine($"        private WorkflowOptions       options;");
            sbSource.AppendLine($"        private ChildWorkflowOptions  childOptions;");
            sbSource.AppendLine($"        private string                taskList;");
            sbSource.AppendLine($"        private string                domain;");
            sbSource.AppendLine($"        private WorkflowExecution     execution;");

            // Generate the constructor used to start an external workflow.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, string workflowTypeName, string taskList, WorkflowOptions options, string domain)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client           = client;");
            sbSource.AppendLine($"            this.dataConverter    = dataConverter;");
            sbSource.AppendLine($"            this.workflowTypeName = workflowTypeName;");
            sbSource.AppendLine($"            this.options          = options ?? new WorkflowOptions();");
            sbSource.AppendLine($"            this.taskList         = ___StubHelpers.ResolveTaskList(client, taskList);");
            sbSource.AppendLine($"            this.domain           = ___StubHelpers.ResolveDomain(client, domain);");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to start a child workflow.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, string workflowTypeName, ChildWorkflowOptions options)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client           = client;");
            sbSource.AppendLine($"            this.dataConverter    = dataConverter;");
            sbSource.AppendLine($"            this.workflowTypeName = workflowTypeName;");
            sbSource.AppendLine($"            this.childOptions     = options ?? new ChildWorkflowOptions();");
            sbSource.AppendLine($"            this.taskList         = ___StubHelpers.ResolveTaskList(client, this.childOptions.TaskList);");
            sbSource.AppendLine($"            this.domain           = ___StubHelpers.ResolveDomain(client, this.childOptions.Domain);");
            sbSource.AppendLine($"        }}");

            // Generate the constructor used to attach to an existing workflow.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, WorkflowExecution execution, string domain)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client        = client;");
            sbSource.AppendLine($"            this.dataConverter = dataConverter;");
            sbSource.AppendLine($"            this.execution     = execution;");
            sbSource.AppendLine($"            this.domain        = ___StubHelpers.ResolveDomain(client, domain);");
            sbSource.AppendLine($"        }}");

            // Generate the method that converts the instance into a new untyped [IWorkflowStub].

            sbSource.AppendLine();
            sbSource.AppendLine($"        public IWorkflowStub ToUntyped()");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            return ___StubHelpers.NewWorkflowStub(client, workflowTypeName, execution, taskList, options, domain);");
            sbSource.AppendLine($"        }}");

            // Generate the properties.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public Workflow Workflow {{ get; set; }}");

            // Generate the workflow entry point methods.

            foreach (var details in methodSignatureToDetails.Values.Where(d => d.Kind == WorkflowMethodKind.Workflow))
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
                sbSource.AppendLine($"            // Configure the workflow.");
                sbSource.AppendLine();

                if (string.IsNullOrEmpty(details.WorkflowMethodAttribute.Name))
                {
                    sbSource.AppendLine($"            var ___workflowTypeName = this.workflowTypeName;");
                }
                else
                {
                    sbSource.AppendLine($"            var ___workflowTypeName = $\"{{this.workflowTypeName}}::{details.WorkflowMethodAttribute.Name}\";");
                }

                sbSource.AppendLine($"            var ___options          = this.options.Clone();");
                sbSource.AppendLine($"            var ___taskList         = this.taskList;");

                if (details.WorkflowMethodAttribute != null)
                {
                    if (!string.IsNullOrEmpty(details.WorkflowMethodAttribute.TaskList))
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (string.IsNullOrEmpty(___taskList))");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___taskList = {StringLiteral(details.WorkflowMethodAttribute.TaskList)};");
                        sbSource.AppendLine($"            }}");
                    }

                    if (details.WorkflowMethodAttribute.ExecutionStartToCloseTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (__options.ExecutionStartToCloseTimeout <= TimeSpan.Zero)");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.ExecutionStartToCloseTimeout = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.ExecutionStartToCloseTimeoutSeconds});");
                        sbSource.AppendLine($"            }}");
                    }

                    if (details.WorkflowMethodAttribute.TaskStartToCloseTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (__options.TaskStartToCloseTimeout <= TimeSpan.Zero)");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.TaskStartToCloseTimeout = TimeSpan.FromSeconds({details.WorkflowMethodAttribute.TaskList});");
                        sbSource.AppendLine($"            }}");
                    }

                    if (!string.IsNullOrEmpty(details.WorkflowMethodAttribute.WorkflowId))
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (string.IsNullOrEmpty(__options.WorkflowId)");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.WorkflowId = {StringLiteral(details.WorkflowMethodAttribute.WorkflowId)};");
                        sbSource.AppendLine($"            }}");
                    }
                }

                sbSource.AppendLine();
                sbSource.AppendLine($"            // Ensure that this stub instance has not already been started.");
                sbSource.AppendLine();
                sbSource.AppendLine($"            if (this.execution != null)");
                sbSource.AppendLine($"            {{");
                sbSource.AppendLine($"                throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] has already been started.\");");
                sbSource.AppendLine($"            }}");
                sbSource.AppendLine();
                sbSource.AppendLine($"            // Start and then wait for the workflow to complete.");
                sbSource.AppendLine();
                sbSource.AppendLine($"            var ___argBytes    = {SerializeArgsExpression(details.Method.GetParameters())};");
                sbSource.AppendLine($"            this.execution     = await ___StubHelpers.StartWorkflowAsync(this.client, ___workflowTypeName, ___argBytes, ___taskList, ___options, this.domain);");
                sbSource.AppendLine($"            var ___resultBytes = await ___StubHelpers.GetWorkflowResultAsync(this.client, this.execution, this.domain);");

                if (!details.IsVoid)
                {
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            return dataConverter.FromData<{resultType}>(___resultBytes);");
                }

                sbSource.AppendLine($"        }}");
            }

            // Generate the workflow signal methods.

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
                sbSource.AppendLine($"            // Ensure that this stub instance has been started.");
                sbSource.AppendLine();
                sbSource.AppendLine($"            if (this.execution == null)");
                sbSource.AppendLine($"            {{");
                sbSource.AppendLine($"                throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] cannot be signalled because the workflow hasn't been started.\");");
                sbSource.AppendLine($"            }}");
                sbSource.AppendLine();
                sbSource.AppendLine($"            var ___argBytes = {SerializeArgsExpression(details.Method.GetParameters())};");
                sbSource.AppendLine();
                sbSource.AppendLine($"            await ___StubHelpers.SignalWorkflowAsync(this.client, this.execution, {StringLiteral(details.SignalMethodAttribute.Name)}, ___argBytes, this.domain);");
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
                sbSource.AppendLine($"            if (this.execution == null)");
                sbSource.AppendLine($"            {{");
                sbSource.AppendLine($"                throw new InvalidOperationException(\"Workflow stub for [{workflowInterface.FullName}] cannot be queried because the workflow hasn't been started.\");");
                sbSource.AppendLine($"            }}");
                sbSource.AppendLine();
                sbSource.AppendLine($"            var ___argBytes    = {SerializeArgsExpression(details.Method.GetParameters())};");
                sbSource.AppendLine($"            var ___resultBytes = await ___StubHelpers.QueryWorkflowAsync(this.client, this.execution, {StringLiteral(details.QueryMethodAttribute.Name)}, ___argBytes, this.domain);");

                if (!details.IsVoid)
                {
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            return dataConverter.FromData<{resultType}>(___resultBytes);");
                }

                sbSource.AppendLine($"        }}");
            }

            // Close out the stub class definition.

            sbSource.AppendLine($"    }}");
            sbSource.AppendLine($"}}");

            var source = sbSource.ToString();

            //-----------------------------------------------------------------
            // Compile the new stub class into an assembly.

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
                    throw new CompilerErrorException(emitted.Diagnostics);
                }
            }

            dllStream.Position = 0;

            //-----------------------------------------------------------------
            // Load the new assembly into the current context.  Note that we're
            // going to do this within a lock because it's possible that we created
            // two stubs for the same workflow interface in parallel and we need
            // to ensure that we're only going to load one of them.

            lock (syncLock)
            {
                if (!workflowInterfaceToStub.TryGetValue(workflowInterface, out stub))
                {
                    var stubAssembly = AssemblyLoadContext.Default.LoadFromStream(dllStream);
                    var stubType     = stubAssembly.GetType(stubFullClassName);

                    stub = new DynamicWorkflowStub(stubType, stubAssembly, stubFullClassName);

                    workflowInterfaceToStub.Add(stubType, stub);
                }
            }

            return (TWorkflowInterface)stub.Create(client, client.DataConverter, workflowTypeName, taskList, options, domain);
        }

        /// <summary>
        /// Creates a dynamically generated stub for the specified activity interface.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="workflow">The parent workflow.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <param name="domain">Optionally specifies the target domain.</param>
        /// <returns>The activity stub instance.</returns>
        public static TActivityInterface CreateActivityStub<TActivityInterface>(CadenceClient client, IWorkflowBase workflow, ActivityOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            var activityInterface = typeof(TActivityInterface);

            CadenceHelper.ValidateActivityInterface(activityInterface);

            options = options ?? new ActivityOptions();

            if (string.IsNullOrEmpty(domain))
            {
                domain = null;
            }

            var activityTypeName  = activityInterface.FullName;
            var activityAttribute = activityInterface.GetCustomAttribute<ActivityAttribute>();

            //-----------------------------------------------------------------
            // Check whether we already have generated a stub class for the interface
            // and return a stub instance right away.

            DynamicActivityStub stub;

            lock (syncLock)
            {
                if (!activityInterfaceToStub.TryGetValue(activityInterface, out stub))
                {
                    stub = null;
                }
            }

            if (stub != null)
            {
                return (TActivityInterface)stub.Create(client, workflow.Workflow, activityTypeName, options, domain);
            }

            //-----------------------------------------------------------------
            // We need to generate the stub class.

            // Scan the interface methods to ensure that all of them implement a 
            // task/async signature by returning a Task.  We're also going to 
            // build a table that maps the method signature to additional details.

            var methodSignatureToDetails = new Dictionary<string, ActivityMethodDetails>();

            foreach (var method in activityInterface.GetMethods())
            {
                if (method.ReturnType.IsGenericType)
                {
                    if (method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new ActivityDefinitionException($"Activity interface method [{activityInterface.FullName}.{method.Name}()] must return a Task.");
                    }
                }
                else
                {
                    if (method.ReturnType != typeof(Task))
                    {
                        throw new ActivityDefinitionException($"Activity interface method [{activityInterface.FullName}.{method.Name}()] must return a Task.");
                    }
                }

                var details = new ActivityMethodDetails()
                {
                    ActivityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>(),
                    Method                  = method
                };

                if (method.ReturnType.IsGenericType)
                {
                    if (method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new ActivityDefinitionException($"Activity interface method [{activityInterface.FullName}.{method.Name}()] must return a Task.");
                    }

                    details.ReturnType = method.ReturnType.GetGenericArguments().First();
                }
                else
                {
                    if (method.ReturnType != typeof(Task))
                    {
                        throw new ActivityDefinitionException($"Activity interface method [{activityInterface.FullName}.{method.Name}()] must return a Task.");
                    }

                    details.ReturnType = typeof(void);
                }

                methodSignatureToDetails.Add(method.ToString(), details);
            }

            // Generate C# source code that implements the stub.  Note that stub classes will
            // be generated within the [Neon.Cadence.Stubs] namespace and will be named by
            // the interface name plus the "Stub_#" suffix where "#" is the number of stubs
            // generated so far.  This will help avoid naming conflicts while still being
            // somewhat readable in debug stack traces.

            var classId       = Interlocked.Increment(ref nextClassId);
            var stubClassName = $"{activityInterface.Name}Stub_{classId}";

            if (stubClassName.Length > 1 && stubClassName.StartsWith("I"))
            {
                stubClassName = stubClassName.Substring(1);
            }

            var stubFullClassName = $"Neon.Cadence.Stubs.{stubClassName}";
            var interfaceFullName = activityInterface.FullName.Replace('+', '.');   // .NET uses "+" for nested types; convert these to "."
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
            sbSource.AppendLine($"using Neon.Common;");
            sbSource.AppendLine($"using Neon.Cadence;");
            sbSource.AppendLine();
            sbSource.AppendLine($"namespace Neon.Cadence.Stubs");
            sbSource.AppendLine($"{{");
            sbSource.AppendLine($"    public class {stubClassName} : {interfaceFullName}");
            sbSource.AppendLine($"    {{");

            AppendStubHelper(sbSource);    // Generate a private static [___StubHelper] class. 

            sbSource.AppendLine();
            sbSource.AppendLine($"        private CadenceClient         client;");
            sbSource.AppendLine($"        private IDataConverter        dataConverter;");
            sbSource.AppendLine($"        private IWorkflowBase         workflow;");
            sbSource.AppendLine($"        private string                activityTypeName;");
            sbSource.AppendLine($"        private ActivityOptions       options;");
            sbSource.AppendLine($"        private string                domain;");

            // Generate the constructor used to execute an activity.

            sbSource.AppendLine();
            sbSource.AppendLine($"        public {stubClassName}(CadenceClient client, IDataConverter dataConverter, IWorkflowBase workflow, string activityTypeName, string ActivityOptions options, string domain)");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            this.client           = client;");
            sbSource.AppendLine($"            this.dataConverter    = dataConverter;");
            sbSource.AppendLine($"            this.workflow         = workflow;");
            sbSource.AppendLine($"            this.activityTypeName = activityTypeName;");
            sbSource.AppendLine($"            this.options          = options ?? new ActivityOptions();");
            sbSource.AppendLine($"            this.options.TaskList = ___StubHelpers.ResolveTaskList(client, options.TaskList);");
            sbSource.AppendLine($"            this.domain           = ___StubHelpers.ResolveDomain(client, domain);");
            sbSource.AppendLine($"        }}");

            // Generate the method that converts the instance into a new untyped [IActivityStub].

            sbSource.AppendLine();
            sbSource.AppendLine($"        public IActivityStub ToUntyped()");
            sbSource.AppendLine($"        {{");
            sbSource.AppendLine($"            return ___StubHelpers.NewActivityStub(client, activityTypeName, execution, taskList, options, domain);");
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
                sbSource.AppendLine($"            // Configure the activity.");
                sbSource.AppendLine();

                if (string.IsNullOrEmpty(details.ActivityMethodAttribute.Name))
                {
                    sbSource.AppendLine($"            var ___activityTypeName = this.activityTypeName;");
                }
                else
                {
                    sbSource.AppendLine($"            var ___activityTypeName = $\"{{this.workflowTypeName}}::{details.ActivityMethodAttribute.Name}\";");
                }

                sbSource.AppendLine($"            var ___options          = this.options.Clone();");

                if (details.ActivityMethodAttribute != null)
                {
                    if (!string.IsNullOrEmpty(details.ActivityMethodAttribute.TaskList))
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (string.IsNullOrEmpty(___options.TaskList))");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___taskList = {StringLiteral(details.ActivityMethodAttribute.TaskList)};");
                        sbSource.AppendLine($"            }}");
                    }

                    if (details.ActivityMethodAttribute.HeartbeatTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (___options.HeartbeatTimeout <= TimeSpan.Zero))");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.HeartbeatTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.HeartbeatTimeoutSeconds});");
                        sbSource.AppendLine($"            }}");
                    }

                    if (details.ActivityMethodAttribute.ScheduleToCloseTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (___options.ScheduleToCloseTimeout <= TimeSpan.Zero))");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.ScheduleToCloseTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.ScheduleToCloseTimeoutSeconds});");
                        sbSource.AppendLine($"            }}");
                    }

                    if (details.ActivityMethodAttribute.ScheduleToStartTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (___options.ScheduleToStartTimeout <= TimeSpan.Zero))");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.ScheduleToStartTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.ScheduleToStartTimeoutSeconds});");
                        sbSource.AppendLine($"            }}");
                    }

                    if (details.ActivityMethodAttribute.StartToCloseTimeoutSeconds > 0)
                    {
                        sbSource.AppendLine();
                        sbSource.AppendLine($"            if (___options.StartToCloseTimeout <= TimeSpan.Zero))");
                        sbSource.AppendLine($"            {{");
                        sbSource.AppendLine($"                ___options.StartToCloseTimeout = TimeSpan.FromSeconds({details.ActivityMethodAttribute.StartToCloseTimeoutSeconds});");
                        sbSource.AppendLine($"            }}");
                    }
                }

                sbSource.AppendLine($"            // Execute the activity.");
                sbSource.AppendLine();
                sbSource.AppendLine($"            var ___argBytes    = {SerializeArgsExpression(details.Method.GetParameters())};");
                sbSource.AppendLine($"            this.execution     = await ___StubHelpers.StartWorkflowAsync(this.client, ___workflowTypeName, ___argBytes, ___taskList, ___options, this.domain);");
                sbSource.AppendLine($"            var ___resultBytes = await ___StubHelpers.GetWorkflowResultAsync(this.client, this.execution, this.domain);");

                if (!details.IsVoid)
                {
                    sbSource.AppendLine();
                    sbSource.AppendLine($"            return dataConverter.FromData<{resultType}>(___resultBytes);");
                }

                sbSource.AppendLine($"        }}");
            }

            // Close out the stub class definition.

            sbSource.AppendLine($"    }}");
            sbSource.AppendLine($"}}");

            var source = sbSource.ToString();

            //-----------------------------------------------------------------
            // Compile the new stub class into an assembly.

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
                    throw new CompilerErrorException(emitted.Diagnostics);
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
                    var stubAssembly = AssemblyLoadContext.Default.LoadFromStream(dllStream);
                    var stubType     = stubAssembly.GetType(stubFullClassName);

                    stub = new DynamicActivityStub(stubType, stubAssembly, stubFullClassName);

                    activityInterfaceToStub.Add(stubType, stub);
                }
            }

            return (TActivityInterface)stub.Create(client, workflow.Workflow, activityTypeName, options, domain);
        }

        /// <summary>
        /// Generates the static <b>___StubHelpers</b> class that exposes the internal
        /// <see cref="CadenceClient"/> and other methods and constructors required by
        /// workflow stubs.  The generated class will use reflection to access these methods.
        /// </summary>
        /// <param name="sbSource">The C# source code being generated.</param>
        private static void AppendStubHelper(StringBuilder sbSource)
        {
            sbSource.Append(
@"        private static class ___StubHelpers
        {
            private static MethodInfo       startWorkflowAsync;             // from: CadenceClient
            private static MethodInfo       getWorkflowDescriptionAsync;    // from: CadenceClient
            private static MethodInfo       getWorkflowResultAsync;         // from: CadenceClient
            private static MethodInfo       cancelWorkflowAsync;            // from: CadenceClient
            private static MethodInfo       terminateWorkflowAsync;         // from: CadenceClient
            private static MethodInfo       signalWorkflowAsync;            // from: CadenceClient
            private static MethodInfo       signalWorkflowWithStartAsync;   // from: CadenceClient
            private static MethodInfo       queryWorkflowAsync;             // from: CadenceClient
            private static MethodInfo       resolveTaskList;                // from: CadenceClient
            private static MethodInfo       resolveDomain;                  // from: CadenceClient
            private static MethodInfo       executeActivityAsync;           // from: Workflow
            private static MethodInfo       executeLocalActivityAsync;      // from: Workflow
            private static ConstructorInfo  newWorkflowStub;                // from: WorkflowStub

            static ___StubHelpers()
            {
                var clientType   = typeof(CadenceClient);
                var workflowType = typeof(Workflow);

                startWorkflowAsync           = NeonHelper.GetMethod(clientType, ""StartWorkflowAsync"", typeof(string), typeof(byte[]), typeof(string), typeof(WorkflowOptions), typeof(string));
                getWorkflowDescriptionAsync  = NeonHelper.GetMethod(clientType, ""GetWorkflowDescriptionAsync"", typeof(WorkflowExecution), typeof(string));
                getWorkflowResultAsync       = NeonHelper.GetMethod(clientType, ""GetWorkflowResultAsync"", typeof(WorkflowExecution), typeof(string));
                cancelWorkflowAsync          = NeonHelper.GetMethod(clientType, ""CancelWorkflowAsync"", typeof(WorkflowExecution), typeof(string));
                terminateWorkflowAsync       = NeonHelper.GetMethod(clientType, ""TerminateWorkflowAsync"", typeof(WorkflowExecution), typeof(string), typeof(byte[]), typeof(string));
                signalWorkflowAsync          = NeonHelper.GetMethod(clientType, ""SignalWorkflowAsync"", typeof(WorkflowExecution), typeof(string), typeof(byte[]), typeof(string));
                signalWorkflowWithStartAsync = NeonHelper.GetMethod(clientType, ""SignalWorkflowWithStartAsync"", typeof(string), typeof(byte[]), typeof(byte[]), typeof(string), typeof(WorkflowOptions), typeof(string));
                queryWorkflowAsync           = NeonHelper.GetMethod(clientType, ""QueryWorkflowAsync"", typeof(WorkflowExecution), typeof(string), typeof(byte[]), typeof(string));
                resolveTaskList              = NeonHelper.GetMethod(clientType, ""ResolveTaskList"", typeof(string));
                resolveDomain                = NeonHelper.GetMethod(clientType, ""ResolveDomain"", typeof(string));

                executeActivityAsync         = NeonHelper.GetMethod(clientType, ""ExecuteActivityAsync"", typeof(string), typeof(byte[]), typeof(ActivityOptions));
                executeLocalActivityAsync    = NeonHelper.GetMethod(clientType, ""ExecuteLocalActivityAsync"", typeof(Type), typeof(MethodInfo), typeof(byte[]), typeof(LocalActivityOptions));

                newWorkflowStub              = NeonHelper.GetConstructor(typeof(WorkflowStub), typeof(CadenceClient), typeof(string), typeof(WorkflowExecution), typeof(string), typeof(WorkflowOptions), typeof(string));
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<WorkflowExecution> StartWorkflowAsync(CadenceClient client, string workflowTypeName, byte[] args, string taskList, WorkflowOptions options, string domain)
            {
                return await (Task<WorkflowExecution>)startWorkflowAsync.Invoke(client, new object[] { workflowTypeName, args, taskList, options, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<WorkflowDescription> GetWorkflowDescriptionAsync(CadenceClient client, WorkflowExecution execution, string domain)
            {
                return await (Task<WorkflowDescription>)getWorkflowDescriptionAsync.Invoke(client, new object[] { execution, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> GetWorkflowResultAsync(CadenceClient client, WorkflowExecution execution, string domain)
            {
                return await (Task<byte[]>)getWorkflowResultAsync.Invoke(client, new object[] { execution, domain });
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
            public static async Task SignalWorkflowWithStartAsync(CadenceClient client, string workflowId, string signalName, byte[] signalArgs, byte[] workflowArgs, string taskList, WorkflowOptions options, string domain)
            {
                await (Task)signalWorkflowWithStartAsync.Invoke(client, new object[] { workflowId, signalName, signalArgs, workflowArgs, taskList, options, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> QueryWorkflowAsync(CadenceClient client, WorkflowExecution execution, string queryType, byte[] queryArgs, string domain)
            {
                return await (Task<byte[]>)queryWorkflowAsync.Invoke(client, new object[] { execution, queryType, queryArgs, domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string ResolveTaskList(CadenceClient client, string taskList)
            {
                return (string)resolveTaskList.Invoke(client, new object[] { taskList });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string ResolveDomain(CadenceClient client, string domain)
            {
                return (string)resolveDomain.Invoke(client, new object[] { domain });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> ExecuteActivityAsync(Workflow workflow, string activityTypeName, byte[] args, ActivityOptions options)
            {
                return await (Task<byte[]>)executeActivityAsync.Invoke(workflow, new object[] { activityTypeName, args, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static async Task<byte[]> ExecuteLocalActivityAsync(Workflow workflow, Type activityType, MethodInfo method, byte[] args, LocalActivityOptions options)
            {
                return await (Task<byte[]>)executeLocalActivityAsync.Invoke(workflow, new object[] { activityType, method, args, options });
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static WorkflowStub NewWorkflowStub(CadenceClient client, string workflowType, WorkflowExecution execution, string taskList, WorkflowOptions options, string domain)
            {
                return (WorkflowStub)newWorkflowStub.Invoke(new object[] { client, workflowType, execution, taskList, options, domain });
            }
        }
");
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
