### Testing Solutions

This folder includes the various unit testing projects.  Most of these are referenced by the main solution at `$/neonKUBE.sln` and are used for testing during development.  These test projects reference the target Neon libraries directly and do not reference the published nuget packages.  This works well when testing on Windows, but will not work when testing on OS/X because builds are currently supported only on Windows.

The **Portable/Portable.sln** solution includes another partial set of test projects that reference the nuget packages and can be run on OS/X or Linux.

**NOTE:** Not all of the existing tests have been added to the **PortableTests.sln** solution.

### Projects and Names

Projects that end with **.net** target .NET Frametwork, projects that end with **.portable** are the portable tests.  All other projects target .NET Core.

Note that the .NET Core projects hold the actual test source files.  The .NET and portable projects simply reference the .NET Core project source files via links.

**NOTE:** You'll need to manually ensure that you keep the linked source files in sync with the source test projects by adding or removing files as necessary.
