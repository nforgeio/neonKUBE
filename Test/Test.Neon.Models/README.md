# NOTE

[jefflill]: I would have liked to use and test the [Neon.ModelGenerator] library here but that won't work because [ModelGenerator] needs to be actually referenced as a nuget package rather than a project reference, due to its nature.

So we'll just test using the [model-gen] client built by the solution before this project due to a configured project dependency.  This is fairly close emulation of what a nuget package based reference would do.
