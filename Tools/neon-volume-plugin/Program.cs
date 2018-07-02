//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
//
// This project is simply used to hold the [neon-volume] GOLANG source files and
// project post-build events are used to actually build the golang SOURCE using
// the [nhive/golang] container.
//
// The compiled [neon-volume] binary will be written to the [bin] project subfolder.
//
// NOTE:
// -----
// You should add all of the GOLANG source files to the project and set the
// build action to CONTENT.  This ensures that the GO project is rebuilt
// whenever any of the source files are changed.

using System;

namespace neon_volume
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This tool is actually just used to build the [neon-volume] GOLANG Docker plugin.");
        }
    }
}
