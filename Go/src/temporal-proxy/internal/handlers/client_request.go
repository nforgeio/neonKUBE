// -----------------------------------------------------------------------------
// FILE:		client_request.go
// CONTRIBUTOR: John C Burns
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
// limitations under the License.

package handlers

import (
	"context"
	"fmt"
	"os"
	"time"

	"go.temporal.io/api/namespace/v1"
	"go.temporal.io/api/workflowservice/v1"
	"go.temporal.io/sdk/client"
	"go.uber.org/zap"

	"temporal-proxy/internal"
	"temporal-proxy/internal/messages"
	proxyclient "temporal-proxy/internal/temporal/client"
)

// ----------------------------------------------------------------------
// IProxyRequest client message type handler methods

func handlePingRequest(requestCtx context.Context, request *messages.PingRequest) messages.IProxyReply {
	Logger.Debug("PingRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new PingReply
	reply := messages.CreateReplyMessage(request)
	reply.Build(nil)

	return reply
}

func handleCancelRequest(requestCtx context.Context, request *messages.CancelRequest) messages.IProxyReply {
	targetID := request.GetTargetRequestID()
	Logger.Debug("CancelRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("TargetId", targetID),
		zap.Int("ProcessId", os.Getpid()))

	// new InitializeReply
	reply := messages.CreateReplyMessage(request)
	reply.Build(nil, true)

	return reply
}

func handleConnectRequest(requestCtx context.Context, request *messages.ConnectRequest) messages.IProxyReply {
	Logger.Debug("ConnectRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ConnectReply
	reply := messages.CreateReplyMessage(request)

	// create and set the logger
	logger := SetLogger(internal.LogLevel, internal.Debug)
	clientHelper := proxyclient.NewHelper()
	clientHelper.Logger = logger.Named(internal.ProxyLoggerName)

	// configure client options
	defaultNamespace := *request.GetNamespace()
	opts := client.Options{
		Identity:  *request.GetIdentity(),
		HostPort:  *request.GetHostPort(),
		Namespace: defaultNamespace,
	}

	// configure the Helper
	// setup the namespace, service, and workflow clients
	err := clientHelper.SetupTemporalClients(requestCtx, opts)

	if err != nil {
		reply.Build(internal.ErrConnection)
		return reply
	}

	// set the timeout
	clientTimeout := request.GetClientTimeout()
	clientHelper.SetClientTimeout(clientTimeout)

	// reset the deadline on ctx with new timeout
	// and check if we need to register the default
	// namespace
	if request.GetCreateNamespace() {
		ctx, cancel := context.WithTimeout(requestCtx, clientTimeout)
		defer cancel()

		// register the namespace
		retention, err := time.ParseDuration("168h")
		if err != nil {
			reply.Build(err)
			return reply
		}

		err = clientHelper.RegisterNamespace(
			ctx,
			&workflowservice.RegisterNamespaceRequest{
				Name:                             defaultNamespace,
				WorkflowExecutionRetentionPeriod: &retention,
			})

		if err != nil {
			reply.Build(err)
			return reply
		}
	}

	_ = Clients.Add(request.GetClientID(), clientHelper)
	reply.Build(nil)

	return reply
}

func handleDisconnectRequest(requestCtx context.Context, request *messages.DisconnectRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	Logger.Debug("DisconnectRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new DisconnectReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// destroy clients
	if err := clientHelper.Destroy(); err != nil {
		Logger.Error("Had trouble disconnecting from Temporal client.",
			zap.Int64("ClientID", clientID),
			zap.Error(err))

		reply.Build(err)
		return reply
	}

	_ = Clients.Remove(clientID)

	Logger.Info("Successfully removed client.", zap.Int64("ClientID", clientID))
	reply.Build(nil)

	return reply
}

func handleHeartbeatRequest(requestCtx context.Context, request *messages.HeartbeatRequest) messages.IProxyReply {
	Logger.Debug("HeartbeatRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new HeartbeatReply
	reply := messages.CreateReplyMessage(request)

	reply.Build(nil)

	return reply
}

func handleInitializeRequest(requestCtx context.Context, request *messages.InitializeRequest) messages.IProxyReply {
	Logger.Info("InitializeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new InitializeReply
	reply := messages.CreateReplyMessage(request)

	// set the reply address
	if internal.DebugPrelaunched {
		replyAddress = "http://127.0.0.1:5001/"
	} else {
		address := *request.GetLibraryAddress()
		port := request.GetLibraryPort()
		replyAddress = fmt.Sprintf(
			"http://%s:%d/",
			address,
			port)
	}

	logLevel := request.GetLogLevel()
	internal.LogLevel = logLevel
	logger := SetLogger(internal.LogLevel, internal.Debug)
	Logger = logger.Named(internal.ProxyLoggerName)

	Logger.Info("Initialization info",
		zap.String("Reply Address", replyAddress),
		zap.String("LogLevel", logLevel.String()))

	reply.Build(nil)

	return reply
}

func handleTerminateRequest(requestCtx context.Context, request *messages.TerminateRequest) messages.IProxyReply {
	Logger.Debug("TerminateRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new TerminateReply
	reply := messages.CreateReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true
	reply.Build(nil)

	return reply
}

func handleNewWorkerRequest(requestCtx context.Context, request *messages.NewWorkerRequest) messages.IProxyReply {
	namespace := *request.GetNamespace()
	taskQueue := *request.GetTaskQueue()
	clientID := request.GetClientID()
	Logger.Debug("NewWorkerRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Namespace", namespace),
		zap.String("TaskQueue", taskQueue),
		zap.Int("ProcessId", os.Getpid()))

	// new NewWorkerReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// configure temporal logger
	// set options
	opts := request.GetOptions()

	// create a new worker using a configured Helper instance

	workerID, err := clientHelper.NewWorker(
		namespace,
		taskQueue,
		*opts)

	if err != nil {
		reply.Build(err, workerID)
		return reply
	}

	Logger.Info("New Worker Created",
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.String("Namespace", namespace),
		zap.String("TaskQueue", taskQueue))

	reply.Build(nil, workerID)

	return reply
}

func handleStartWorkerRequest(requestCtx context.Context, request *messages.StartWorkerRequest) messages.IProxyReply {
	workerID := request.GetWorkerID()
	clientID := request.GetClientID()
	Logger.Debug("StartWorkerRequest Received",
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new StartWorkerReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the workerID from the request so that we know
	// what worker to stop

	if clientHelper.StartWorker(workerID) != nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	Logger.Info("Worker successfully started",
		zap.Int64("WorkerID", workerID),
		zap.Int64("ClientId", clientID))

	reply.Build(nil)

	return reply
}

func handleStopWorkerRequest(requestCtx context.Context, request *messages.StopWorkerRequest) messages.IProxyReply {
	workerID := request.GetWorkerID()
	clientID := request.GetClientID()
	Logger.Debug("StopWorkerRequest Received",
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new StopWorkerReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the workerID from the request so that we know
	// what worker to stop

	if clientHelper.StopWorker(workerID) != nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	Logger.Info("Worker has been removed from Workers",
		zap.Int64("WorkerID", workerID),
		zap.Int64("ClientId", clientID))

	reply.Build(nil)

	return reply
}

func handleNamespaceDescribeRequest(requestCtx context.Context, request *messages.NamespaceDescribeRequest) messages.IProxyReply {
	namespace := *request.GetName()
	clientID := request.GetClientID()
	Logger.Debug("NamespaceDescribeRequest Received",
		zap.String("Namespace", namespace),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new NamespaceDescribeReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// send a describe namespace request to the temporal server
	describeNamespaceResponse, err := clientHelper.DescribeNamespace(ctx, namespace)
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, describeNamespaceResponse)

	return reply
}

func handleNamespaceRegisterRequest(requestCtx context.Context, request *messages.NamespaceRegisterRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	namespaceName := *request.GetName()
	Logger.Debug("NamespaceRegisterRequest Received",
		zap.String("Namespace", namespaceName),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new NamespaceRegisterReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create a new temporal namespace RegisterNamespaceRequest for
	// registering a new namespace
	registerNamespaceRequest := workflowservice.RegisterNamespaceRequest{
		Name:        namespaceName,
		Description: *request.GetDescription(),
		OwnerEmail:  *request.GetOwnerEmail(),
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// register the namespace using the RegisterNamespaceRequest
	err := clientHelper.RegisterNamespace(ctx, &registerNamespaceRequest)
	if err != nil {
		Logger.Error("failed to register namespace",
			zap.String("Namespace Name", namespaceName),
			zap.Error(err))

		reply.Build(err)

		return reply
	}

	reply.Build(nil)

	return reply
}

func handleNamespaceUpdateRequest(requestCtx context.Context, request *messages.NamespaceUpdateRequest) messages.IProxyReply {
	nspace := *request.GetName()
	clientID := request.GetClientID()
	Logger.Debug("NamespaceUpdateRequest Received",
		zap.String("Namespace", nspace),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new NamespaceUpdateReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	//Config
	configuration := request.GetNamespaceConfig()
	updateInfo := namespace.UpdateNamespaceInfo{
		Description: *request.GetUpdatedInfoDescription(),
		OwnerEmail:  *request.GetUpdatedInfoOwnerEmail(),
	}

	// NamespaceUpdateRequest
	namespaceUpdateRequest := workflowservice.UpdateNamespaceRequest{
		Name:       nspace,
		Config:     configuration,
		UpdateInfo: &updateInfo,
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// Update the namespace using the UpdateNamespaceRequest
	err := clientHelper.UpdateNamespace(ctx, &namespaceUpdateRequest)
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleDescribeTaskQueueRequest(requestCtx context.Context, request *messages.DescribeTaskQueueRequest) messages.IProxyReply {
	name := *request.GetName()
	namespace := *request.GetNamespace()
	clientID := request.GetClientID()
	Logger.Debug("DescribeTaskQueueRequest Received",
		zap.String("TaskQueue", name),
		zap.String("Namespace", namespace),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new DescribeTaskQueueReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	describeResponse, err := clientHelper.DescribeTaskQueue(ctx, namespace, name, request.GetTaskQueueType())
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, describeResponse)

	return reply
}
