# Cadence Proxy

The Cadence Proxy is simply a GOLANG executable which will be cross-compiled for the three important target environments: Windows, Linux, and OSX. Neon.Cadence is a .NET Standard 2.0 library that will embed the three Cadence executables as binary resources. .NET applications will reference the Neon.Cadence library and then make calls to establish Cadence Server connections, manage namespaces, define workflows, and then register workflow and activity handlers.

## Cadence Proxy Lifecycle

The Neon.Cadence library manages the lifecyle of the Cadence Proxy process in production, something like this:

1. The .NET application instantiates a Neon.Cadence client instance to establish a connection to a Cadence Server.

2. If this is the first Cadence connection, then the Neon.Cadence library will write the Cadence Proxy resource binary that's appropriate for the current platform to disk (if the executable doesn't already exist) and start it, specifing the network endpoint the Cadence Proxy is expected to listen on. For subsequent client connections, the library will start another Cadence Proxy instance using the existing executable file.

3. After the Cadence Library starts the proxy, it will send it a ConnectRequest message specifying the network endpoint where the library is listening as well as the details for the Cadence cluster being targeted (more details below). The proxy will sent a ConnectReply message back to the library indicating success or failure.

4. As the .NET application performs various operations via the library, the library will submit the appropriate requests to the proxy via HTTP to its networkl endpoint.

5. Eventually the .NET application will close its Cadence server connection. The Neon.Cadence library signal the Cadence Proxy to terminate itself by sending it a TerminateRequest message.

## Cadence Proxy Command Line

The Neon.Cadence library will persist Cadence Proxy executable to disk using a file name appropriate for the current platform: cadence-proxy.exe for Windows and just cadence-proxy for Linux and OSX and then start the proxy via a command like like:

```
cadence-proxy 127.0.0.1:5555
```

where the argument specifies the network endpoint where the Cadence Proxy will listen for requests.

## Cadence Client/Proxy Communication 

The client and proxy will communicate on a local loopback address via HTTP with each side deploying an HTTP server. Cadence proxy messages can be sent in either direction. The .NET client and proxy are tightly coupled, so the binary message format and protocol is not designed to support backwards compatibility, since these two components will always be packaged and deployed together.

The low-level Client/proxy communication protocol is one-way: the client can send messages to the proxy and the proxy can send messages to the client, but other than the HTTP 200 response (or 400/404/405 for invalid requests). The server may return an error string in the response content for 400/404/405 responses. In general though, operation replies are not returned in the PUT response. The query/reply pattern will be implemented asynchronously at a higher level.

## Cadence Library/Proxy Endpoint Paths

The Cadence Library and Proxy will both listen for HTTP PUT requests on a specified IP address and port and each of these will initially support two endpoint URI paths:

* "/": Is where normal communication between the library and proxy takes place. Messages PUT to here will be process and if the request is valid, result in the PUT of a reply to the other side.

* "/echo": Is a special testing endpoint. Messages PUT to this endpoint are to be deserialized, copied to a new message of the same type which should be serialized and returned as the PUT response. This is used for integration to verify that both the library and proxy have implemented serialization correctly. This endpoint performs no other function.

## Cadence Terminology

* Workflow: These orchestrate one or more activities each of which performs an idempotent task. Workflows are started with zero or more parameters and then schedule a sequence of activities based on the input parameters and the results from already completed activities.

* Activity: These perform the actual work for a workflow. Activities are fundamentally idempotent and Cadence ensures that any given activity is executed only once and that its results are persisted so that the same results will be returned when an already-completed activity is replayed.

* Signal: Cadence provides a way to asynchronously send signals to a workflow. Signals are named by a string and also include a string as the payload.

* Signal Channel: Workflows configure themselves to receive signals by creating a signal channel named for the desired signal.

## Cadence Proxy Message Format

This formats designed to be a very simple and flexible way of communicating operations and status between the Cadence client and proxy. The specific message type is identified via the Type property (one of the MessageType values. The Args dictionary will be used to pass named values. Binary attachments may be passed using the Attachments property, a list of binary arrays.

This is serialized to bytes using a simple structure consisting of 32-bit integers, UTF-8 encoded strings, and raw bytes with all integers encoded using little-endian byte ordering. Strings are encoded as a 32-bit byte length, followed by that many UTF-8 encoded string bytes. A ZERO byte length indicates an empty string and a length of -1 indicates a NULL string. Encoded strings will look like:

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
|  PROPERTY-COUNT  |   32-bit
+------------------+
|  +------------+  |
|  |  NAME-LEN  |  |   32-bit
|  +------------+  |
|  |   NAME     |  |
|  +------------+  |
|  |  VALUE-LEN |  |   32-bit
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
The message starts out with the 32-bit message type followed by the number of properties to follow. Each property consists of an encoded string for the property name followed by an encoded string for the value.

After the properties will be a 32-bit integer specifying the number of binary attachment with each encoded as its length in bytes followed by that actual attachment bytes. An attachment with length set to -1 will be considered to be NULL.

Proxy messages will be passed between the Cadence client and proxy via PUT requests using the application/x-neon-cadence-proxy content-type. Note that request HTTP responses in both directions never include any content.

NOTE: It is possible that some properties may not be serialized for a message. This should be intepreted at the low-level as if the property value was serialized as NULL and at higher levels as if the property was set to the default value for the type (e.g. NULL for strings, FALSE for booleans, 0 for integers, etc).

## Common Property Type Serialization
Both the Cadence Proxy and Library will need to agree on how certain common property types will be formatted as strings within message properties. Here's what we're going to do for various types:

* boolean: These will be serialized as either "true" or "false" (lowercase).

* integer: These will be serialized as you'd expect with an optional leading "-" for negative numbers. Note that a leading "+" is not allowed.

* floating point: These will be serialized like 1.2345 with an optional leading "+-" sign. There must be at least one digit before the decimal point. Note that the decimal point must be omitted if there are no fractional digits and that the decimal point must be a period (not a comma or anything else used for other cultures). Invalid numbers will be serialized as "NaN". Scientific notation is also supported.

* date/time: We're going to use a varient of RFC 3389 for serializing date/time values. All date/times will be rendered relative to UTC and will be formatted like: "yyyy-MM-ddTHH:mm:ss.fffZ" to provide millisecond precision.

* time span: We're going to express these as a 64-bit integer number of ticks, where a single tick equals 100 nanoseconds (the .NET standard).

## Request and Reply Messages

The Cadence Client and Cadence Proxy communicate by transmitting request and reply messages. Either side initiates an operation by sending a request message to its peer and when the peer completes the operation or there's an error, the peer will transmit a reply message back to the original sender. Note that reply messages are separate HTTP PUT requests. Reply messages are not returned as the HTTP response to a request message.

Request messages transmitted from either side are submitted using the HTTP PUT method. The receiving side will generally respond with an 200 (OK) status code or 400 (Bad Request) if the proxy message could not be parsed. A message sender that receives a 200 response will expect the target to respond with a corresponding reply message in the future. In production, we should never see a 400 response, because that probable means there's a bug in the serialization code on one side or the other.

Reply messages transmitted from either side work the same. They're transmitted as PUT requests and the sender will expect a 200 or 400 status code as the response.

All request and response messages will include a RequestId property the uniquely identifies the specific request. This will simply be a 64-bit integer encoded. The Cadence Client and Cadence Proxy will each maintain a global nextRequestId variable that will be incremented (protected by a mutex) every time a new request message is sent. Each reply message will include the same RequestId received with the corresponding request message. This will be used by the reqiesting side to correlate received replies with the original request.

Reply messages will also two more standard properties:

* string ErrorType: This will be non-NULL if the requested operation failed due to an error. This will be one of the following strings indicating the type of error encountered:

    * cancelled
    * custom
    * generic
    * panic
    * terminated
    * timeout
 
* string ErrorMessage: This string describes the error in more detail.

## Message Type Hierarchy 

This section discusses how proxy messages are organized using common base message classes.

NOTE: For the sections below, we're going to specify request and response messages using a minimized C# class syntax with the idea that these will be serialized into the message format described above. These classes will actually be implemented within the Neon.Cadence library and we should also do the equivalent on GOLANG for within the Cadence Proxy.

The Cadence library and the Cadence Proxy will communicate using the message types listed here:

[Message Types](https://github.com/nforgeio/neonKUBE/blob/master/Lib/Neon.Cadence/MessageTypes.cs "Message Types")

### ProxyMessage

The ProxyMessage class is inherited by all other message types and provides basic serialization related functionality.

```
type ProxyMessage struct {
    Type 		    MessageType
    Properties      map[string]*string          
    Attachments		[][]byte
}
```

These properties map closely to the serialized message format described above.

### ProxyRequest

The ProxyRequest class is inherited by all request messages. Its purpose is to add the RequestId property that is common across all requests.

```
type ProxyRequest struct {
    *ProxyMessage
}
```

### ProxyReply

The ProxyReply class is inherited by all reply messages. Its purpose is to add the RequestId and optional error related properties.

```
type ProxyReply struct {
    *ProxyMessage
}
```

### Workflow Messages

All Workflow related messages derive from either ProxyWorkflowRequest or ProxyWorkflowReply. These classes add the ContextId property which is used to identify the workflow context associated with the message.

```
type WorkflowProxyRequest struct {
    *ProxyRequest
}

type WorkflowProxyReply struct {
    *ProxyReply
}
```

### Activity Messages

All Activity related messages derive from either ProxyActivityRequest or ProxyActivityReply. These classes add the ContextId property which is used to identify the activity context associated with the message.

NOTE: In theory we could have shared a common implementation with the workflow request/reply messages but we're separating these because they might diverge in the future and I don't want to have inheritence nest too deeply.

```
type ActivityProxyRequest struct {
    *ProxyRequest
}

type ActivityProxyReply struct {
    *ProxyReply
}
```

## Project Structure

The cadence-proxy project follows the [golang-standard project layout](https://github.com/golang-standards/project-layout "Golang Standard Project-Layout GitHub"):
```
cadence-proxy/
    bin/
        cadence-proxy.linux
        cadence-proxy.osx
        cadence-proxy.win.exe 
    cmd/
        cadenceproxy/
            cadenceclient/
                cadence_error_types.go
                cadence_client_helper.go
                cadence_client_factory.go
            endpoints/
                cadence_proxy_endpoints.go
                test_endpoints.go
            messages/
                activity/
                    message_reply.go
                    message_request.go
                base/
                    proxy_message.go
                    proxy_message_request.go
                    proxy_message_reply.go
                connect/
                    message_request.go
                    message_reply.go
                domain/
                    describe_message_request.go
                    describe_message_reply.go
                    register_message_request.go
                    register_message_reply.go
                    update_message_reply.go
                    update_message_request.go
                initialize/
                    message_request.go
                    message_reply.go
                terminate/
                    message_request.go
                    message_reply.go
                workflow/
                    message_request.go
                    message_reply.go
            server/
                instance.go
                router.go
            main.go
        test/
            cadence-proxy.test
            logs/
                test.logs
        vendor/
            */
            */
            */
        Gopkg.lock
        Gopkg.toml
        Makefile
        README.md
```


# Running and Maintaining the Cadence Proxy

The cadence-proxy is written in Golang and needs to be built into windows, linux, and OSX executables.  You must have Golang installed on your machine to build the Golang executables.  If you do not have Golang installed, check out [install Golang](https://golang.org/dl/ "Installing Golang") and for Windows machines, download and install the Microsoft Windows `.msi`.  Follow the installation instructions on the [download page](https://golang.org/doc/install?download=go1.12.4.windows-amd64.msi "The Go Programming Language: Getting Started"). 

## Building the cadence-proxy for neonKUBE.sln Build

Building the cadence-proxy Golang executables is part of the neonKUBE.sln build.  This happens in `$NF_ROOT/Go/build-cadence-proxy.ps1`, which is a powershell script that builds a cadence-proxy Golang executable for windows, linux, and OSX.  The [neonKUBE](https://github.com/nforgeio/neonKUBE "nforgeio/neonKUBE") repository includes source code for all Go dependencies necessary for building the cadence-proxy Golang executables, so there are no further steps required for compiling and building the executables.  These dependencies can be found in `$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy/vendor`.  The `vendor` directory and `$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy/Gopkg.lock` are generated, by the Golang tool [dep](https://github.com/golang/dep "dep GitHub").  Dep is the dependency management tool for Go and is the convention for managing dependencies in Golang projects.  Upon successful completion of the build script, 3 executables, `cadence-proxy.win.exe`, `cadence-proxy.linux`, and `cadence-proxy.osx` are placed in `$NF_ROOT/Build`.

## Building the cadence-proxy for development on the cadence-proxy

If you are developing the cadence-proxy Golang project, you might need to manage the cadence-proxy project dependencies themselves.  This requires a different method of building that utilizes a `Makefile` in the cadence-proxy project root (`$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy`).  The `Makefile` uses Golang's dependency management tool, dep, to install new project dependencies and update existing ones.  Because dep is used, you must have it installed in your `$GOPATH`.  To do this run the command:

```
go get -u github.com/golang/dep/cmd/dep
```

This will install or update the dep package in your machine's `$GOPATH`.  Running the `Makefile` will also run all test files in the cadence-proxy project.  It will also generate a Golang test executable in `$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy/test`, as well as test logs in `$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy/test/logs`.  To execute the `Makefile`, run the command:

```
make
```
in the cadence-proxy project root (`$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy`).  In order to run `make`, you must have a console that can run the command.  [Cmder](https://cmder.net/ "Cmder.net") is a good console emulator for windows that provides some nice extra console functionality (like `make`) not included in the windows Command Prompt.  Upon successful completion, the Golang executables will be placed into `$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy/bin`.  To run the windows executable, simply change directories from the cadence-proxy project root to `$NF_ROOT/Go/src/github.com/loopieio/cadence-proxy/bin`, and run in the console:

```
cadence-proxy.win.exe
```
The executable should run and the cadence-proxy server will start up.  

### Steps Before Committing

Before committing any work on the cadence-proxy, first you must build, test, and then clean up your mess: Golang executables, test files, log files, build files, etc.

1. Run `make` in the cadence-proxy project root.  Make sure the tests pass and the executables are built.
2. Once the tests pass and the executables are built in `bin`, execute the OS specific executable.
3. If you want to, you can run some sort of integration test to make sure that the server is working as it is supposed to.
4. Shut down the server.
5. Run `make clean` in the cadence-proxy project root.  This will remove test files, logs, build files, and the generated executables.
6. Look through the `Gopkg.lock` file and document any version changes in the dependencies, or any new dependencies.  
7. Commit

  

