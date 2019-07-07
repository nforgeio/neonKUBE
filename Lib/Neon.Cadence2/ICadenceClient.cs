//-----------------------------------------------------------------------------
// FILE:	    ICadenceClient.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a client that will be connected to a Cadence cluster and be used
    /// to create and manage workflows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To get started with Cadence, you'll need to deploy a Cadence cluster with
    /// one or more nodes and the establish a connection to the cluster from your
    /// workflow/activity implementations and management tools.  This is pretty
    /// easy to do.
    /// </para>
    /// <para>
    /// First, you'll need to know the URI of at least one of the Cadence cluster
    /// nodes.  Cadence listens on port <b>79133</b> by default so you cluster URIs
    /// will typically look like: <b>http://CADENCE-NODE:7933</b>.
    /// </para>
    /// <note>
    /// For production clusters with multiple Cadence nodes, you should specify
    /// multiple URIs when connecting just in case the one of the nodes may be
    /// offline for some reason.
    /// </note>
    /// <para>
    /// To establish a connection, you'll construct a <see cref="CadenceSettings"/>
    /// and add your node URIs to the <see cref="CadenceSettings.Servers"/> list
    /// and then call the static <see cref="CadenceClient.ConnectAsync(CadenceSettings)"/>
    /// method to obtain a connected <see cref="CadenceClient"/>.  You'll use this
    /// for registering workflows and activities types as well as the workers that
    /// indicate that workflows and activities can be executed in the current process.
    /// </para>
    /// <para>
    /// You'll implement your workflows and activities by implementing classes that
    /// derive from <see cref="WorkflowBase"/> and <see cref="ActivityBase"/> and then
    /// registering these types with Cadence.  Then you'll start workflow or activity
    /// workers so that Cadence will begin scheduling operations for execution by your code.
    /// Workflows and activities are registered using the fully qualified names 
    /// of the derived <see cref="WorkflowBase"/> and <see cref="ActivityBase"/> types
    /// by defaut, but you can customize this if desired.
    /// </para>
    /// <para>
    /// Cadence supports the concept of domains and task lists.  Domains and task lists are
    /// used to organize workflows and activities.  Workflows and activities essentially 
    /// reside in a registered domain, which is essentially just a namespace specified by
    /// a string.  The combination of a domain along with a workflow or activity type name
    /// must be unique within a Cadence cluster.  Once you have a connected <see cref="CadenceClient"/>,
    /// you can create and manage Cadence domains via methods like <see cref="RegisterDomainAsync(string, string, string, int, bool)"/>,
    /// <see cref="DescribeDomainAsync(string)"/>, and <see cref="UpdateDomainAsync(string, DomainUpdateArgs)"/>.
    /// Domains can be used provide isolated areas for different teams and/or different environments
    /// (e.g. production, staging, and test).  We discuss task lists in detail further below.
    /// </para>
    /// <para>
    /// Cadence workers are started to indicate that the current process can execute workflows
    /// and activities from a Cadence domain, and optionally a task list (discussed further below).
    /// You'll call <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/> to start
    /// a workflow worker and  <see cref="StartActivityWorkerAsync(string, string, WorkerOptions)"/>
    /// for an activity worker.  These calls indicate to Cadence that it can begin scheduling
    /// workflow and activity executions from the current client.
    /// </para>
    /// <para>
    /// Worflows are implemented by deriving a class from <see cref="WorkflowBase"/> and activities
    /// are implemented by deriving a class from <see cref="ActivityBase"/>.  These classes
    /// require the implementation of the <see cref="WorkflowBase.RunAsync(byte[])"/> and
    /// <see cref="ActivityBase.RunAsync(byte[])"/> methods that actually implement the workflow
    /// and activity logic.  After establishing a connection ot a Cadence cluster, you'll need
    /// to call <see cref="CadenceClient.RegisterWorkflowAsync{TWorkflow}(string)"/> and/or
    /// <see cref="CadenceClient.RegisterActivityAsync{TActivity}(string)"/> to register your
    /// workflow and activity implementations with Cadence.  These calls combined with the
    /// workers described above determine which workflows and activities may be scheduled
    /// on the current client/process.
    /// </para>
    /// <para>
    /// For situations where you have a lot of workflow and activity classes, it can become
    /// combersome to register each implementation class individually (generally because you
    /// forget to register new classes after they've been implemented).  To assist with this,
    /// you can also tag your workflow and activity classes with <see cref="AutoRegisterAttribute"/>
    /// and then call <see cref="CadenceClient.RegisterAssemblyWorkflowsAsync(Assembly)"/> and/or
    /// <see cref="CadenceClient.RegisterAssemblyActivitiesAsync(Assembly)"/> to scan an assembly and
    /// automatically register the tagged implementation classes it finds.
    /// </para>
    /// <para>
    /// Next you'll need to start workflow and/or activity workers.  These indicate to Cadence that 
    /// the current process implements specific workflow and activity types.  You'll call
    /// <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/> for
    /// workflows and <see cref="StartActivityWorkerAsync(string, string, WorkerOptions)"/>
    /// for activities, passing your custom implementations of <see cref="WorkflowBase"/> and <see cref="ActivityBase"/>
    /// as the type parameter.  The <b>Neon.Cadence</b> will then automatically handle the instantiation
    /// of your workflow or activity types and call their <see cref="WorkflowBase.RunAsync(byte[])"/>
    /// </para>
    /// <para>
    /// External or top-level workflows are started by calling <see cref="StartWorkflowAsync(string, byte[], string, string, WorkflowOptions)"/> 
    /// or <see cref="StartWorkflowAsync{TWorkflow}(byte[], string, string, WorkflowOptions)"/>, passing the workflow 
    /// type string, the target Cadence domain along with optional arguments (encoded into a byte array) 
    /// and optional workflow options.  The workflow type string must be the same one used when calling 
    /// <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/>.
    /// </para>
    /// <note>
    /// <b>External workflows</b> are top-level workflows that have no workflow parent.
    /// This is distinugished from <b>child workflows</b> that are executed within the
    /// context of another workflow via <see cref="WorkflowBase.CallChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken)"/>.
    /// </note>
    /// <para>
    /// <see cref="StartWorkflowAsync(string, byte[], string, string, WorkflowOptions)"/> returns
    /// immediately after the workflow is submitted to Cadence and the workflow will be scheduled and
    /// executed independently.  This method returns a <see cref="WorkflowExecution"/> which you'll use
    /// to identify your running workflow to the methods desribed below.
    /// </para>
    /// <para>
    /// You can monitor the status of an external workflow by polling <see cref="GetWorkflowStateAsync(WorkflowExecution)"/>
    /// or obtain a workflow result via <see cref="GetWorkflowResultAsync(WorkflowExecution)"/>, which blocks until the 
    /// workflow completes.
    /// </para>
    /// <note>
    /// Child workflows and activities are started from within a <see cref="WorkflowBase"/> implementation
    /// via the <see cref="WorkflowBase.CallChildWorkflowAsync{TWorkflow}(byte[], ChildWorkflowOptions, CancellationToken)"/>,
    /// <see cref="WorkflowBase.CallChildWorkflowAsync(string, byte[], ChildWorkflowOptions, CancellationToken)"/>,
    /// <see cref="WorkflowBase.CallActivityAsync{TActivity}(byte[], ActivityOptions, CancellationToken)"/>
    /// <see cref="WorkflowBase.CallActivityAsync(string, byte[], ActivityOptions, CancellationToken)"/>, and
    /// <see cref="WorkflowBase.CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions, CancellationToken)"/>
    /// methods.
    /// </note>
    /// <para>
    /// Workflows can be signalled via <see cref="SignalWorkflowAsync(string, string, byte[], string)"/> or
    /// <see cref="SignalWorkflowWithStartAsync(string, string, byte[], byte[], string, WorkflowOptions)"/> that starts the
    /// workflow if its not already running.  You can query running workflows via 
    /// <see cref="QueryWorkflowAsync(string, string, byte[], string)"/>.
    /// </para>
    /// <para>
    /// Workflows can be expicitly closed using <see cref="RequestCancelWorkflowExecution(WorkflowExecution)"/>,
    /// <see cref="TerminateWorkflowAsync(WorkflowExecution, string, byte[])"/>.
    /// </para>
    /// <para><b>Restarting Workflows</b></para>
    /// <para>
    /// Long running workflows that are essentially a high-level loop can result in the recording
    /// of an excessive number of events to its history.  This can result in poor performance
    /// due to having to replay this history when the workflow has to be rehydrated.  
    /// </para>
    /// <para>
    /// You can avoid this by removing the workflow loop and calling <see cref="WorkflowBase.RestartAsync(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// at the end of your workflow logic.  This causes Cadence to reschedule the workflow
    /// with a clean history, somewhat similar to what happens for CRON workflows (which are
    /// rescheduled automatically).  <see cref="WorkflowBase.RestartAsync(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// works by throwing a <see cref="CadenceWorkflowRestartException"/> which will exit
    /// the workflow method and be caught by the calling <see cref="CadenceClient"/> which
    /// which then informs Cadence.
    /// </para>
    /// <note>
    /// Workflow entry points must allow the <see cref="CadenceWorkflowRestartException"/> to be caught by the
    /// calling <see cref="CadenceClient"/> so that <see cref="WorkflowBase.RestartAsync(byte[], string, string, TimeSpan, TimeSpan, TimeSpan, TimeSpan, CadenceRetryPolicy)"/>
    /// will work properly.
    /// </note>
    /// <para><b>External Activity Completion</b></para>
    /// <para>
    /// Normally, activities are self-contained and will finish whatever they're doing and then
    /// simply return.  It's often useful though to be able to have an activity kickoff operations
    /// on an external system, exit the activity indicating that it's still pending, and then
    /// have the external system manage the activity heartbeats and report the activity completion.
    /// </para>
    /// <para>
    /// To take advantage of this, you'll need to obtain the opaque activity identifier from
    /// <see cref="ActivityBase.Info"/> via its <see cref="ActivityInfo.TaskToken"/> property.
    /// This is a byte array including enough information for Cadence to identify the specific
    /// activity.  Your activity should start the external action, passing the task token and
    /// then call <see cref="ActivityBase.CompleteExternallyAsync()"/> which will thrown a
    /// <see cref="CadenceActivityExternalCompletionException"/> that will exit the activity 
    /// and then be handled internally by informing Cadence that the activity will continue
    /// running.
    /// </para>
    /// <note>
    /// You should not depend on the structure or contents of the task token since this
    /// may change for future Cadence releases and you must allow the <see cref="CadenceActivityExternalCompletionException"/>
    /// to be caught by the calling <see cref="CadenceClient"/> so <see cref="ActivityBase.CompleteExternallyAsync()"/>
    /// will work properly.
    /// </note>
    /// <para><b>Arguments and Results</b></para>
    /// <para>
    /// The <b>Neon.Cadence</b> library standardizes on having workflow and activity arguments
    /// and results represented as byte arrays or <c>null</c>.  This is a bit of a simplication
    /// over the Cadence GOLANG client package, which can accept zero or more typed parameters.
    /// <b>Neon.Cadence</b> applications will need to encode any arguments or results into byte 
    /// arrays.  You can use any method to accompilish this, including serializing to JSON via
    /// the <b>Newtonsoft.Json</b> nuget package.
    /// </para>
    /// <para><b>Task Lists</b></para>
    /// <para>
    /// Task lists provide an additional way to customize where workflows and activities are executed.
    /// A task list is simply a string used in addition to the domain to indicate which workflows and
    /// activities will be scheduled for execution by workers.  For regular (top-level) workflows,
    /// the task list <b>"default"</b> will be used when not otherwise specified.  Any non-empty custom
    /// string is allowed for task lists.  Child workflow and activity task lists will default to
    /// the parent workflow's task list by default.
    /// </para>
    /// <para>
    /// Task lists are typically only required for somewhat advanced deployments.  Let's go through
    /// an example to see how this works.  Imagine that you're a movie studio that needs to render
    /// an animated movie with Cadence.  You've implemented a workflow that breaks the movie up into
    /// 5 minute segments and then schedules an activity to render each segment.  Now assume that 
    /// we have two kinds of servers, one just a basic general purpose server and the other that
    /// includes high-end GPUs that are required for rendering.  In the simple case, you'd like
    /// the workflows to run on the regular server and the activites to run on the GPU machines
    /// (because there's no point in wasting any expensive GPU machine resources on the workflow).
    /// </para>
    /// <para>
    /// This scenario can addressed by having the applications running on the regular machines
    /// call <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/> and those
    /// running on the GPU servers call <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/>.
    /// Both could specify the domain as <b>"render"</b> and leave task list as <b>"default"</b>.
    /// With this setup, workflows will be scheduled on the regular machines and activities
    /// on the GPU machines, accomplishing our simple goal.
    /// </para>
    /// <para>
    /// Now imagine a more complex scenario where we need to render two movies on the cluster at 
    /// the same time and we'd like to dedicate two thirds of our GPU machines to <b>movie1</b> and
    /// the other third to <b>movie2</b>.  This can be accomplished via task lists:
    /// </para>
    /// <para>
    /// We'd start by defining a task list for each movie: <b>"movie1"</b> and <b>movie2</b> and
    /// then call <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/> twice on
    /// the regular machines, once for each task list.  This will schedule workflows for each movie
    /// on these machines (this is OK for this scenario because the workflow won't consume many
    /// resources).  Then on 2/3s of the GPU machines, we'll call <see cref="StartActivityWorkerAsync(string, string, WorkerOptions)"/>
    /// with the <b>"movie1"</b> task list and the remaining one third of the GPU machines with
    /// <b>""movie2</b> as the task list.  Then we'll start the rendering workflow for the first
    /// movie specifying <b>"movie1"</b> as the task list and again for the second movie specifying 
    /// <b>"movie2"</b>.
    /// </para>
    /// <para>
    /// The two movie workflows will be scheduled on the regular machines and these will each
    /// start the rendering activities using the <b>"movie1"</b> task list for the first movie
    /// and <b>"movie2"</b> for the second one and Cadence will then schedule these activities
    /// on the appropriate GPU servers.
    /// </para>
    /// <para>
    /// This was just one example.  Domains and task lists can be combined in different ways
    /// to manage where workflows and activities execute.
    /// </para>
    /// </remarks>
    public partial interface ICadenceClient : IDisposable
    {
        /// <summary>
        /// Returns the settings used to create the client.
        /// </summary>
        CadenceSettings Settings { get; }

        /// <summary>
        /// Returns the URI the client is listening on for requests from the <b>cadence-proxy</b>.
        /// </summary>
        Uri ListenUri { get; }

        /// <summary>
        /// Returns the URI the associated <b>cadence-proxy</b> instance is listening on.
        /// </summary>
        Uri ProxyUri { get; }

        /// <summary>
        /// Raised when the connection is closed.  You can determine whether the connection
        /// was closed normally or due to an error by examining the <see cref="CadenceClientClosedArgs"/>
        /// arguments passed to the handler.
        /// </summary>
        event CadenceClosedDelegate ConnectionClosed;
    }
}