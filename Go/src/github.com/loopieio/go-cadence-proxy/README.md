This issue describes how we're going to implement a .NET Cadence Client.  Currently, Cadence has golang and Java clients, but .NET isn't supported due to the lack of a Thrift library for .NET.  There's been some talk by the Cadence folks about adding HTTP and/or gRPC support to Cadence server but actually doing that doesn't seem to be a priority.

The essential idea here is to write a .NET client library that starts a golang app that acts as a proxy to Cadence server and use HTTP to communicate between the .NET library and the golang app.  The .NET library will embed Windows, Linux, and OSX versions of the golang proxy binary as resources and will write it to disk first and then launch it when a .NET application establishes the first connection for an application.  The golang process started will continue to run and handle any new connections.  The .NET client will terminate the golang process when the last connection is closed by the application.

### Terminology

* **Cadence Client:** Is the .NET Cadence client we're building
* **Cadence Proxy:** The golang program acting as the shim between the Cadence Client and the actual Cadence server.
* **Cadence Proxy Message:** or just _proxy message_, are binary messages used for communication between the cadence client and the cadence proxy.

### Cadence Client/Proxy Communication

The client and proxy will communicate on a local loopback address via HTTP with each side deploying an HTTP server.  Cadence proxy messages can be sent in either direction.  The .NET client and proxy are tightly coupled, so the binary message format and protocol is not designed to support backwards compatibility, since these two components will always be packaged and deployed together.

The low-level Client/proxy communication protocol is **one-way**: the client can send messages to the proxy and the proxy can send messages to the client, but other than the HTTP 200 response, no additional data will be returned in message responses.  The query/response pattern will be implemented at a higher level.

### Cadence Terminology

* **Workflow:** These orchestrate one or more _activities_ each of which performs an idempotent task.  Workflows are started with zero or more parameters and then schedule a sequence of activities based on the input parameters and the results from already completed activities.

* **Activity:** These perform the actual work for a workflow.  Activities are fundamentally idempotent and Cadence ensures that any given activity is executed only once and that its results are persisted so that the same results will be returned when an already-completed activity is replayed.

* **Signal:** Cadence provides a way to asynchronously send signals to a workflow.  Signals are named by a string and also include a string as the payload.

* **Signal Channel:** Workflows configure themselves to receive signals by creating a signal channel named for the desired signal.

### Cadence Proxy Message Format

This formats designed to be a very simple and flexible way of communicating operations and status between the Cadence client and proxy.  The specific  message type is identified via the `Type` property (one of the `MessageType` values.  The `Args` dictionary will be used to pass named values.  Binary attachments may be passed using the  `Attachments` property, a list of binary arrays.

This is serialized to bytes using a simple structure consisting of 32-bit  integers, UTF-8 encoded strings, and raw bytes with all integers encoded using little-endian byte ordering.  Strings are encoded as a 32-bit byte length, followed by that many UTF-8 encoded string bytes.  A ZERO byte length indicates an empty string and a length of -1 indicates a NULL string.  Encoded strings will look like:
```
+------------------+
|      LENGTH      |   32-bit (little endian)
+------------------+
|                  |
|      UTF-8       |
|      BYTES       |
|                  |
+------------------+
```
A full encoded message will look like:
```
+------------------+
|   MESSAGE-TYPE   |   32-bit
+------------------+
|    ARG-COUNT     |   32-bit
+------------------+
|                  |
|  +------------+  |
|  |   NAME     |  |
|  +------------+  |
|  |   VALUE    |  |
|  +------------+  |
|       ...        |
|                  |
+------------------+
|   ATTACH-COUNT   |   32-bit
+------------------+
|                  |
|  +------------+  |
|  |   LENGTH   |  |   32-bit
|  +------------+  |
|  |            |  |
|  |            |  |
|  |   BYTES    |  |
|  |            |  |
|  |            |  |
|  +------------+  |
|       ...        |
|                  |
+------------------+
```
The message starts out with the 32-bit message type followed by the number of arguments to follow.  Each argument consists of an encoded string for the argument name followed by an encoded string for the value.

After the arguments will be a 32-bit integer specifying the number of binary attachment with each encoded as its length in bytes followed by that actual attachment bytes.  An attachment with length set to -1 will be considered to be NULL.

Proxy messages will be passed between the Cadence client and proxy via <b>PUT</b> requests using the **application/x-neon-cadence-proxy** content-type.  Note that request responses in both directions never include any content.
