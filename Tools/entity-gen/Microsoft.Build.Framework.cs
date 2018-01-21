//-----------------------------------------------------------------------------
// FILE:	    Microsoft.Build.Framework.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
//
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

#pragma warning disable 1591 // Disable code comment warnings

namespace Microsoft.Build.Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredAttribute : Attribute
    {
        public RequiredAttribute()
        {
        }
    }

    public interface ITaskHost
    {
    }

    public interface ITask
    {
        IBuildEngine BuildEngine { get; set; }

        ITaskHost HostObject { get; set; }

        bool Execute();
    }

    public class CustomBuildEventArgs : EventArgs
    {
        public CustomBuildEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }

    public class BuildErrorEventArgs : EventArgs
    {
        public BuildErrorEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }

    public class BuildMessageEventArgs : EventArgs
    {
        public BuildMessageEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }

    public class BuildWarningEventArgs : EventArgs
    {
        public BuildWarningEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }

    public interface IBuildEngine
    {
        void LogCustomEvent(CustomBuildEventArgs e);

        void LogErrorEvent(BuildErrorEventArgs e);

        void LogMessageEvent(BuildMessageEventArgs e);

        void LogWarningEvent(BuildWarningEventArgs e);
    }
}
