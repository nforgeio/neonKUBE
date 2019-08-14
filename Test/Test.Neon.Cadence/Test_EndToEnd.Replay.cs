//-----------------------------------------------------------------------------
// FILE:        Test_Messages.Replay.cs
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
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCadence
{
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------
        // Private replay related types and fields.

        /// <summary>
        /// Workflow options to be used for replay testing.  This allows
        /// a large number of retries with only a one second wait between
        /// retries.
        /// </summary>
        private static WorkflowOptions replayWorkflowOptions =
            new WorkflowOptions()
            {
                RetryOptions = new RetryOptions() 
                { 
                    InitialInterval    = TimeSpan.FromSeconds(1),
                    BackoffCoefficient = 1.0,
                    MaximumAttempts    = 120
                }
            };

        /// <summary>
        /// Used to force a workflow to fail so that it can be retried.
        /// </summary>
        public class ForceReplayException : Exception
        {
            public ForceReplayException()
                : base("Forcing workflow replay.")
            {
            }
        }

        /// <summary>
        /// Thrown by <see cref="ReplayManager"/> when a workflow decision
        /// replay from history doesn't match the original decision value.
        /// </summary>
        public class ReplayFailedException : Exception
        {
            public static ReplayFailedException Null { get; private set; } = null;

            public ReplayFailedException(string message)
                : base(message)
            {
            }

            public override string ToString()
            {
                return $"{nameof(ReplayFailedException)}: {this.Message}\r\n\r\n{StackTrace}";
            }
        }

        /// <summary>
        /// Used to help manage workflow replay testing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This class is intended to be used in workflows to verify that each
        /// decision step first executes with [IsReplaying=false] and any result
        /// is recorded.  Then, the workflow will be caused to be replayed by
        /// throwing a <see cref="ForceReplayException"/> and Cadence will restart
        /// the workflow, replaying each of the completed workflow steps from
        /// the history.
        /// </para>
        /// <para>
        /// This class will recognize when decisions are being replayed and ensure
        /// that any reply results from history match the result from each live
        /// decision step.  A <see cref="ReplayFailedException"/> will be thrown
        /// if the replay state is not valid or the expected decision state doesn't
        /// match.
        /// </para>
        /// <para>
        /// This class is pretty easy to use.  Create an instance for each test workflow
        /// class and save it as a static property.  Then have your test method call
        /// <see cref="Reset"/> before actually starting the test.  This resets the
        /// step count and recorded state.
        /// </para>
        /// <para>
        /// The test workflow implementation will be a series of calls to <see cref="NextAsync(Action)"/>
        /// for decision tasks that don't return a value and <see cref="NextAsync{T}(Func{T})"/> for 
        /// tasks that do return a value.  These methods will ensure that the replay state
        /// is correct as well as ensuring the recorded values match the original values.
        /// </para>
        /// <para>
        /// These <see cref="NextAsync(Action)"/> method calls will advance the workflow
        /// one step at a time, effectively causing the workflow to restart and replay
        /// from history after each advancing step.  After the last call to <see cref="NextAsync(Action)"/>,
        /// the workflow will complete and return normally, finishing the test.
        /// </para>
        /// <para>
        /// You're workflow methods should return <see cref="ReplayFailedException"/> and 
        /// have a <c>try/catch</c> block to capture and return this exception, which indicates
        /// a failure.  The method should return <c>null</c> on success.  The unit test method
        /// should throw any non-null exception returned to report any failures.
        /// </para>
        /// <para>
        /// We're going to call <see cref="Workflow.NewGuidAsync"/> after every test decision
        /// task (and ignore the results) to start a new decision task that we can cause to
        /// fail so the workflow will be retried.
        /// </para>
        /// </remarks>
        public class ReplayManager
        {
            /// <summary>
            /// Identifies the currently completed decision step.  Zero indicates
            /// that the workflow hasn't been started yet.
            /// </summary>
            public int Step { get; private set; } = 0;

            /// <summary>
            /// Holds decision state so that live state recorded and then compared
            /// to state replayed from history.
            /// </summary>
            public List<object> History { get; private set; } = new List<object>();

            /// <summary>
            /// Resets for a new test run.
            /// </summary>
            public void Reset()
            {
                Step = 0;
                History.Clear();
            }

            /// <summary>
            /// Used to invoke the next workflow step that returns a result.
            /// </summary>
            /// <typeparam name="T">The step's result type.</typeparam>
            /// <param name="workflow">The workflow state.</param>
            /// <param name="decision">The decision step implementation.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            public async Task NextAsync(Workflow workflow, Func<Task> decision)
            {
                Covenant.Requires<ArgumentNullException>(workflow != null);
                Covenant.Requires<ArgumentNullException>(decision != null);

                if (workflow.IsReplaying)
                {
                    if (Step <= History.Count)
                    {
                        throw new ReplayFailedException($"Workflow step {Step} failed because Cadence reports that it is replaying but it hasn't executed yet.");
                    }

                    await decision();
                    Step++;
                    await workflow.NewGuidAsync();  // Starts a new decision task
                }
                else
                {
                    await decision();
                    History.Add(null);
                    Step++;
                    await workflow.NewGuidAsync();  // Starts a new decision task

                    throw new ForceReplayException();
                }
            }

            /// <summary>
            /// Used to invoke the next workflow step that doesn't return a result.
            /// </summary>
            /// <typeparam name="T">The step's result type.</typeparam>
            /// <param name="workflow">The workflow state.</param>
            /// <param name="decision">The decision step implementation.</param>
            /// <returns>The decision step result.</returns>
            public async Task<T> NextAsync<T>(Workflow workflow, Func<Task<T>> decision)
            {
                Covenant.Requires<ArgumentNullException>(workflow != null);
                Covenant.Requires<ArgumentNullException>(decision != null);

                T result;

                if (workflow.IsReplaying)
                {
                    if (Step <= History.Count)
                    {
                        throw new ReplayFailedException($"Workflow step [{Step}] failed because Cadence reports that it is replaying but it hasn't executed yet.");
                    }

                    result = await decision();

                    if (!NeonHelper.JsonEquals(History[Step], result))
                    {
                        throw new ReplayFailedException($"STEP [{Step}]: Replay result doesn't match the original value.");
                    }

                    Step++;
                    await workflow.NewGuidAsync();  // Starts a new decision task
                }
                else
                {
                    result = await decision();
                    History.Add(result);
                    Step++;
                    await workflow.NewGuidAsync();  // Starts a new decision task

                    //throw new ForceReplayException();
                    await Task.Delay(TimeSpan.FromSeconds(12));
                }

                return result;
            }
        }

        //---------------------------------------------------------------------

        public interface IWorkflowReplayStart : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowReplayStart : WorkflowBase, IWorkflowReplayStart
        {
            private static bool alreadyStarted = false;

            public static void Reset()
            {
                alreadyStarted = false;
            }

            public async Task<bool> RunAsync()
            {
                // Verify that workflows start off not replaying and then
                // will indicate replay when restarted.  We're going to 
                // force a replay by throwing an exception on the first
                // invoke.
                //
                // The workflow returns TRUE if the replay state was value.

                if (!alreadyStarted)
                {
                    alreadyStarted = true;

                    if (Workflow.IsReplaying)
                    {
                        // We should NOT be replaying because this is the first invoke.

                        return await Task.FromResult(false);
                    }

                    throw new ForceReplayException();
                    // await Task.Delay(TimeSpan.FromSeconds(12));

                    return await Task.FromResult(false);
                }
                else
                {
                    if (!Workflow.IsReplaying)
                    {
                        // We SHOULD be replaying because this is the NOT first invoke.

                        return await Task.FromResult(false);
                    }

                    return await Task.FromResult(false);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Replay_Start()
        {
            // Verifies that the a workflow initially starts out as NOT replaying
            // and then after an exception, will indicate that it IS replaying.

            WorkflowReplayStart.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplayStart>(replayWorkflowOptions);
            
            Assert.True(await stub.RunAsync());
        }

#if TODO
        //---------------------------------------------------------------------

        public interface IWorkflowReplayStart : IWorkflow
        {
            [WorkflowMethod]
            Task<ReplayFailedException> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowReplayStart : WorkflowBase, IWorkflowReplayStart
        {
            internal static ReplayManager ReplayManager = new ReplayManager();

            public async Task<ReplayFailedException> RunAsync()
            {
                // Verify that workflows start off not replaying and then
                // will indicate replay when restarted.

                try
                {
                    await ReplayManager.NextAsync(this.Workflow, async () => await Workflow.NewGuidAsync());
                }
                catch (ReplayFailedException e)
                {
                    return await Task.FromResult(e);
                }

                return await Task.FromResult(ReplayFailedException.Null);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Replay_Start()
        {
            WorkflowReplayStart.ReplayManager.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplayStart>(replayWorkflowOptions);
            var error = await stub.RunAsync();

            if (error != null)
            {
                throw error;
            }
        }
#endif
    }
}
