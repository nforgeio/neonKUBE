using System;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyDescription("This is a minor modification of the Renci.SSHNET package to workaround a hang.  I'm hoping this will be a temporary thing until the next stable version of SSH.NET is released.")]
[assembly: AssemblyCompany("Renci")]
[assembly: AssemblyProduct("SSH.NET")]
[assembly: AssemblyCopyright("Copyright © Renci 2010-2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("2016.1.0")]
[assembly: AssemblyFileVersion("2016.1.0")]
[assembly: AssemblyInformationalVersion("2016.1.0-beta1")]
[assembly: CLSCompliant(false)]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]


#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif