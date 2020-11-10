Neon.Cadence
============

---

**NOTE:** We're going to be **deprecating support for Uber Cadence in the near future** and will be transitioning to the new fork created by [temporal.io](https://temporal.io/).

[temporal.io](https://temporal.io/) was founded by **Maxim Fateev** who started the Cadence effort at Uber as well as the Simple Workflow Foundation (SWF) project at Amazon AWS.  We expect to see most if not all significant future advancements for this platform to be comming from Temporal.

We are actively engaged in porting **Neon.Cadence** to Temporal and you can follow our efforts here: [Neon.Temporal](https://github.com/nforgeio/neonKUBE/tree/master/Lib/Neon.Temporal)

---

A .NET client for the Uber Cadence workflow platform.

We have been running simple workflows in production using the **Neon.Cadence** client for nearly a year now.  This is lacking a few somewhat advanced features like being able to wait on multiple operations simultaneously in a workflow as well as an in-process workflow engine for better unit testing support. Our current plan is to add these features to the nacent **Neon.Temporal** port.  We're not planning on back porting these to **Neon.Cadence**.

You can get started here: [Neon.Cadence](https://doc.neonkube.com/Neon.Cadence-Overview.htm)
