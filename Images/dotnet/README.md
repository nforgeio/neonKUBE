# Image Tags

These images are tagged using the corresponding version from the Microsoft repo plus the image build date.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Details

neonHIVE base runtime images for Microsoft .NET Core Linux containers.  These images are based off of the corresponding basic **Alpine ASP.NET runtime** images at [microsoft/dotnet](https://hub.docker.com/r/microsoft/dotnet/).  Use the larger [nhive/aspnet](https://hub.docker.com/r/nhive/aspnet/) image if you need ASP.NET support.

# Additional Packages

This image includes the following packages:

* [tini](https://github.com/krallin/tini) is a simple init manager that can be used to ensure that zombie processes are reaped and that Linux signals are forwarded to sub-processes.
