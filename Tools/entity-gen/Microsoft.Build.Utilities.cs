//-----------------------------------------------------------------------------
// FILE:	    Microsoft.Build.Utilities.cs
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

// For some reason, I can reference the [Microsoft.Build.Framework] and [Microsoft.Build.Utilities]
// Nuget packages by the application won't run (it's unable to locate the assemblies.  I suspect
// that this is a Visual Studio 2017 RC issue that will hopefully be resolved for it goes RTM.
// I'll report this to MSFT.
//
// Hack enough of these classes until we can come back and really integrate with MSBUILD.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;

using Microsoft.Build.Framework;

#pragma warning disable 1591 // Disable code comment warnings

namespace Microsoft.Build.Utilities
{
    public abstract class Task : ITask
    {
        protected Task()
        {
            Log = new TaskLoggingHelper(this);
        }

        public IBuildEngine BuildEngine { get; set; }

        //public IBuildEngine2 BuildEngine2 { get; }

        //public IBuildEngine3 BuildEngine3 { get; }

        //public IBuildEngine4 BuildEngine4 { get; }

        public ITaskHost HostObject { get; set; }

        public TaskLoggingHelper Log { get; }

        //protected string HelpKeywordPrefix { get; set; }

        //protected ResourceManager TaskResources { get; set; }

        public abstract bool Execute();
    }

    public class TaskLoggingHelper
    {
        Task task;

        public TaskLoggingHelper(ITask taskInstance)
        {
            task = (Task)taskInstance;
        }

        public bool HasLoggedErrors { get; private set; }

        protected IBuildEngine BuildEngine { get; }

        public void LogError(string message, params object[] messageArgs)
        {
            task.BuildEngine.LogErrorEvent(new BuildErrorEventArgs(string.Format(message, messageArgs)));
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            task.BuildEngine.LogMessageEvent(new BuildMessageEventArgs(string.Format(message, messageArgs)));
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            task.BuildEngine.LogWarningEvent(new BuildWarningEventArgs(string.Format(message, messageArgs)));
        }
    }
}
