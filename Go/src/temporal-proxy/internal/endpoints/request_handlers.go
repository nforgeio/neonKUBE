// -----------------------------------------------------------------------------
// FILE:		request_handlers.go
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

package endpoints

import (
	"bytes"
	"context"
	"fmt"
	"os"
	"reflect"
	"time"

	"github.com/gogo/protobuf/types"
	"go.temporal.io/temporal-proto/namespace"
	"go.temporal.io/temporal-proto/workflowservice"
	"go.temporal.io/temporal/activity"
	"go.temporal.io/temporal/client"
	"go.temporal.io/temporal/encoded"
	"go.temporal.io/temporal/worker"
	"go.temporal.io/temporal/workflow"
	"go.uber.org/zap"

	"temporal-proxy/internal"
	"temporal-proxy/internal/messages"
	proxyactivity "temporal-proxy/internal/temporal/activity"
	proxyclient "temporal-proxy/internal/temporal/client"
	proxyworkflow "temporal-proxy/internal/temporal/workflow"
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

	// configure client options

	defaultNamespace := *request.GetNamespace()
	opts := client.Options{
		Identity:  *request.GetIdentity(),
		HostPort:  *request.GetHostPort(),
		Namespace: defaultNamespace,
	}

	// create and set the logger
	logger := SetLogger(internal.LogLevel, internal.Debug)
	clientHelper := proxyclient.NewClientHelper()
	clientHelper.Logger = logger.Named(internal.ProxyLoggerName)

	// configure the ClientHelper
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
		retention := int32(7)
		err = clientHelper.RegisterNamespace(
			ctx,
			&workflowservice.RegisterNamespaceRequest{
				Name:                                   defaultNamespace,
				WorkflowExecutionRetentionPeriodInDays: retention,
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
	taskList := *request.GetTaskList()
	clientID := request.GetClientID()
	Logger.Debug("NewWorkerRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Namespace", namespace),
		zap.String("TaskList", taskList),
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
	logger := SetLogger(internal.LogLevel, internal.Debug)
	opts := request.GetOptions()
	opts.Logger = logger.Named(internal.TemporalLoggerName)

	// create a new worker using a configured ClientHelper instance

	workerID, err := clientHelper.StartWorker(
		namespace,
		taskList,
		*opts)

	if err != nil {
		reply.Build(err, workerID)
		return reply
	}

	Logger.Info("New Worker Created",
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.String("Namespace", namespace),
		zap.String("TaskList", taskList))

	reply.Build(nil, workerID)

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
	emitMetrics := request.GetEmitMetrics()
	retentionDays := request.GetRetentionDays()
	registerNamespaceRequest := workflowservice.RegisterNamespaceRequest{
		Name:                                   namespaceName,
		Description:                            *request.GetDescription(),
		OwnerEmail:                             *request.GetOwnerEmail(),
		EmitMetric:                             emitMetrics,
		WorkflowExecutionRetentionPeriodInDays: retentionDays,
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

	// NamespaceUpdateRequest.Configuration
	configurationEmitMetrics := request.GetConfigurationEmitMetrics()
	configurationRetentionDays := request.GetConfigurationRetentionDays()
	configuration := namespace.NamespaceConfiguration{
		EmitMetric:                             &types.BoolValue{Value: configurationEmitMetrics},
		WorkflowExecutionRetentionPeriodInDays: configurationRetentionDays,
	}

	// NamespaceUpdateRequest.UpdatedInfo
	updatedInfo := namespace.UpdateNamespaceInfo{
		Description: *request.GetUpdatedInfoDescription(),
		OwnerEmail:  *request.GetUpdatedInfoOwnerEmail(),
	}

	// NamespaceUpdateRequest
	namespaceUpdateRequest := workflowservice.UpdateNamespaceRequest{
		Name:          nspace,
		Configuration: &configuration,
		UpdatedInfo:   &updatedInfo,
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

func handleDescribeTaskListRequest(requestCtx context.Context, request *messages.DescribeTaskListRequest) messages.IProxyReply {
	name := *request.GetName()
	namespace := *request.GetNamespace()
	clientID := request.GetClientID()
	Logger.Debug("DescribeTaskListRequest Received",
		zap.String("TaskList", name),
		zap.String("Namespace", namespace),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new DescribeTaskListReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	describeResponse, err := clientHelper.DescribeTaskList(ctx, namespace, name, request.GetTaskListType())
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, describeResponse)

	return reply
}

// ----------------------------------------------------------------------
// IProxyRequest workflow message type handler methods

func handleWorkflowRegisterRequest(requestCtx context.Context, request *messages.WorkflowRegisterRequest) messages.IProxyReply {
	workflowName := *request.GetName()
	clientID := request.GetClientID()
	workerID := request.GetWorkerID()
	Logger.Debug("WorkflowRegisterRequest Received",
		zap.String("Workflow", workflowName),
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowRegisterReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create workflow function
	workflowFunc := func(ctx workflow.Context, input []byte) ([]byte, error) {
		contextID := proxyworkflow.NextContextID()
		requestID := NextRequestID()
		Logger.Debug("Executing Workflow",
			zap.String("Workflow", workflowName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// set the WorkflowContext in WorkflowContexts
		wectx := proxyworkflow.NewWorkflowContext(ctx)
		wectx.SetWorkflowName(&workflowName)
		contextID = WorkflowContexts.Add(contextID, wectx)

		// Send a WorkflowInvokeRequest to the Neon.Temporal Lib
		// temporal-client
		workflowInvokeRequest := messages.NewWorkflowInvokeRequest()
		workflowInvokeRequest.SetRequestID(requestID)
		workflowInvokeRequest.SetContextID(contextID)
		workflowInvokeRequest.SetArgs(input)
		workflowInvokeRequest.SetClientID(clientID)

		// get the WorkflowInfo (Namespace, WorkflowID, RunID, WorkflowType,
		// TaskList, ExecutionStartToCloseTimeout)
		// from the context
		workflowInfo := workflow.GetInfo(ctx)
		workflowInvokeRequest.SetNamespace(&workflowInfo.Namespace)
		workflowInvokeRequest.SetWorkflowID(&workflowInfo.WorkflowExecution.ID)
		workflowInvokeRequest.SetRunID(&workflowInfo.WorkflowExecution.RunID)
		workflowInvokeRequest.SetWorkflowType(&workflowInfo.WorkflowType.Name)
		workflowInvokeRequest.SetTaskList(&workflowInfo.TaskListName)
		workflowInvokeRequest.SetExecutionStartToCloseTimeout(time.Duration(int64(workflowInfo.WorkflowExecutionTimeoutSeconds) * int64(time.Second)))

		// set ReplayStatus
		setReplayStatus(ctx, workflowInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send workflowInvokeRequest
		go sendMessage(workflowInvokeRequest)

		Logger.Debug("WorkflowInvokeRequest sent",
			zap.String("Workflow", workflowName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// block and get result
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			if isForceReplayErr(s) {
				panic("force-replay")
			}

			Logger.Error("Workflow Failed With Error",
				zap.String("Workflow", workflowName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Workflow Completed Successfully",
				zap.String("Workflow", workflowName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Unexpected result type",
				zap.String("Workflow", workflowName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	clientHelper.WorkflowRegister(workerID, workflowFunc, workflowName)
	Logger.Debug("workflow successfully registered", zap.String("WorkflowName", workflowName))
	reply.Build(nil)

	return reply
}

func handleWorkflowExecuteRequest(requestCtx context.Context, request *messages.WorkflowExecuteRequest) messages.IProxyReply {
	workflowName := *request.GetWorkflow()
	namespace := *request.GetNamespace()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowExecuteRequest Received",
		zap.String("WorkflowName", workflowName),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Namespace", namespace),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowExecuteReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check for options
	var opts client.StartWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// signalwithstart the specified workflow
	workflowRun, err := clientHelper.ExecuteWorkflow(
		ctx,
		namespace,
		opts,
		workflowName,
		request.GetArgs())

	if err != nil {
		reply.Build(err)
		return reply
	}

	workflowExecution := workflow.Execution{
		ID:    workflowRun.GetID(),
		RunID: workflowRun.GetRunID(),
	}

	reply.Build(nil, &workflowExecution)

	return reply
}

func handleWorkflowCancelRequest(requestCtx context.Context, request *messages.WorkflowCancelRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowCancelRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowCancelReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// cancel the specified workflow
	err := clientHelper.CancelWorkflow(
		ctx,
		workflowID,
		runID,
		*request.GetNamespace())

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleWorkflowTerminateRequest(requestCtx context.Context, request *messages.WorkflowTerminateRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowTerminateRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowTerminateReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context to terminate the workflow
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// terminate the specified workflow
	err := clientHelper.TerminateWorkflow(
		ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetNamespace(),
		*request.GetReason(),
		request.GetDetails())

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleWorkflowSignalWithStartRequest(requestCtx context.Context, request *messages.WorkflowSignalWithStartRequest) messages.IProxyReply {
	workflow := *request.GetWorkflow()
	workflowID := *request.GetWorkflowID()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowSignalWithStartRequest Received",
		zap.String("Workflow", workflow),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSignalWithStartReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// signalwithstart the specified workflow
	workflowExecution, err := clientHelper.SignalWithStartWorkflow(
		ctx,
		workflowID,
		*request.GetNamespace(),
		*request.GetSignalName(),
		request.GetSignalArgs(),
		*request.GetOptions(),
		workflow,
		request.GetWorkflowArgs())

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, workflowExecution)

	return reply
}

func handleWorkflowSetCacheSizeRequest(requestCtx context.Context, request *messages.WorkflowSetCacheSizeRequest) messages.IProxyReply {
	Logger.Debug("WorkflowSetCacheSizeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSetCacheSizeReply
	reply := messages.CreateReplyMessage(request)

	// set the sticky workflow cache size
	worker.SetStickyWorkflowCacheSize(request.GetSize())
	reply.Build(nil)

	return reply
}

func handleWorkflowMutableRequest(requestCtx context.Context, request *messages.WorkflowMutableRequest) messages.IProxyReply {
	Logger.Debug("WorkflowMutableRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowMutableReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// f function for workflow.MutableSideEffect
	mutableFunc := func(ctx workflow.Context) interface{} {
		return request.GetResult()
	}

	// the equals function for workflow.MutableSideEffect
	equals := func(a, b interface{}) bool {
		if v, ok := a.(*internal.TemporalError); ok {
			if _v, _ok := b.(*internal.TemporalError); _ok {
				if v.GetType() == _v.GetType() &&
					v.Error() == _v.Error() {
					return true
				}
				return false
			}
			return false
		}

		if v, ok := a.([]byte); ok {
			if _v, _ok := b.([]byte); _ok {
				return bytes.Equal(v, _v)
			}
			return false
		}
		return false
	}

	// MutableSideEffect/SideEffect calls
	var value encoded.Value
	if mutableID := request.GetMutableID(); mutableID != nil {
		value = workflow.MutableSideEffect(
			ctx,
			*mutableID,
			mutableFunc,
			equals)
	} else {
		value = workflow.SideEffect(ctx, mutableFunc)
	}

	// extract the result
	var result []byte
	err := value.Get(&result)

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleWorkflowDescribeExecutionRequest(requestCtx context.Context, request *messages.WorkflowDescribeExecutionRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowDescribeExecutionRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowDescribeExecutionReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// DescribeWorkflow call to temporal client
	dwer, err := clientHelper.DescribeWorkflowExecution(
		ctx,
		workflowID,
		runID,
		*request.GetNamespace())

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, dwer)

	return reply
}

func handleWorkflowGetResultRequest(requestCtx context.Context, request *messages.WorkflowGetResultRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	Logger.Debug("WorkflowGetResultRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowGetResultReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// call GetWorkflow
	workflowRun, err := clientHelper.GetWorkflow(
		ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetNamespace())

	if err != nil {
		reply.Build(err)
		return reply
	}

	// get the result of WorkflowRun
	var result []byte
	err = workflowRun.Get(requestCtx, &result)
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleWorkflowSignalSubscribeRequest(requestCtx context.Context, request *messages.WorkflowSignalSubscribeRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	signalName := *request.GetSignalName()
	Logger.Debug("WorkflowSignalSubscribeRequest Received",
		zap.String("SignalName", signalName),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", request.GetContextID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSignalSubscribeReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx := wectx.GetContext()

	// create selector for receiving signals
	var signalArgs []byte
	selector := workflow.NewSelector(ctx)
	selector = selector.AddReceive(workflow.GetSignalChannel(ctx, signalName), func(channel workflow.ReceiveChannel, more bool) {
		channel.Receive(ctx, &signalArgs)
		Logger.Debug("SignalReceived",
			zap.String("Siganl", signalName),
			zap.Int64("ClientId", clientID),
			zap.ByteString("args", signalArgs))

		// create the WorkflowSignalInvokeRequest
		requestID := NextRequestID()
		workflowSignalInvokeRequest := messages.NewWorkflowSignalInvokeRequest()
		workflowSignalInvokeRequest.SetRequestID(requestID)
		workflowSignalInvokeRequest.SetContextID(contextID)
		workflowSignalInvokeRequest.SetSignalArgs(signalArgs)
		workflowSignalInvokeRequest.SetSignalName(&signalName)
		workflowSignalInvokeRequest.SetClientID(clientID)

		// set ReplayStatus
		setReplayStatus(ctx, workflowSignalInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowSignalInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(workflowSignalInvokeRequest)

		// wait to be unblocked
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("signal failed with error",
				zap.String("Signal", signalName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s))

		case bool:
			Logger.Info("signal completed successfully",
				zap.String("Signal", signalName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Bool("Success", s))

		default:
			Logger.Error("signal result unexpected",
				zap.String("Signal", signalName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s))
		}
	})

	// Subscribe to named signal
	workflow.Go(ctx, func(ctx workflow.Context) {
		var err error
		var done bool
		selector = selector.AddReceive(ctx.Done(), func(c workflow.ReceiveChannel, more bool) {
			err = ctx.Err()
			done = true
		})

		// keep select spinning,
		// looking for requests
		for {
			selector.Select(ctx)
			if err != nil {
				Logger.Error("Error In Workflow Context", zap.Error(err))
			}
			if done {
				return
			}
		}
	})

	return reply
}

func handleWorkflowSignalRequest(requestCtx context.Context, request *messages.WorkflowSignalRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	clientID := request.GetClientID()
	signalName := *request.GetSignalName()
	Logger.Debug("WorkflowSignalRequest Received",
		zap.String("Signal", signalName),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSignalReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context to signal the workflow
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// signal the specified workflow
	err := clientHelper.SignalWorkflow(
		ctx,
		workflowID,
		runID,
		*request.GetNamespace(),
		signalName,
		request.GetSignalArgs())

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleWorkflowHasLastResultRequest(requestCtx context.Context, request *messages.WorkflowHasLastResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowHasLastResultRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowHasLastResultReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)
	reply.Build(nil, workflow.HasLastCompletionResult(ctx))

	return reply
}

func handleWorkflowGetLastResultRequest(requestCtx context.Context, request *messages.WorkflowGetLastResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowGetLastResultRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowGetLastResultReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set replay status
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// get the last completion result from the temporal client
	var result []byte
	err := workflow.GetLastCompletionResult(ctx, &result)
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleWorkflowDisconnectContextRequest(requestCtx context.Context, request *messages.WorkflowDisconnectContextRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowDisconnectContextRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowDisconnectContextReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create a new disconnected context
	// and then replace the existing one with the new one
	disconnectedCtx, cancel := workflow.NewDisconnectedContext(wectx.GetContext())
	wectx.SetContext(disconnectedCtx)
	wectx.SetCancelFunction(cancel)

	reply.Build(nil)

	return reply
}

func handleWorkflowGetTimeRequest(requestCtx context.Context, request *messages.WorkflowGetTimeRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowGetTimeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowGetTimeReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set replay status
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	reply.Build(nil, workflow.Now(ctx))

	return reply
}

func handleWorkflowSleepRequest(requestCtx context.Context, request *messages.WorkflowSleepRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	Logger.Debug("WorkflowSleepRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSleepReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// pause the current workflow for the specified duration
	var result interface{}
	future := workflow.NewTimer(ctx, request.GetDuration())

	// Send ACK: Commented out because its no longer needed.
	// op := sendFutureACK(contextID, requestID, clientID)
	// <-op.GetChannel()

	// wait for the future to be unblocked
	err := future.Get(ctx, &result)
	if err != nil {
		reply.Build(internal.NewTemporalError(err, internal.CancelledError))
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleWorkflowExecuteChildRequest(requestCtx context.Context, request *messages.WorkflowExecuteChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	workflowName := *request.GetWorkflow()
	Logger.Debug("WorkflowExecuteChildRequest Received",
		zap.String("Workflow", workflowName),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowExecuteChildReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// check if replaying
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// set options on the context
	var opts workflow.ChildWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// set cancellation on the context
	// execute the child workflow
	ctx = workflow.WithChildOptions(ctx, opts)
	ctx = workflow.WithScheduleToStartTimeout(ctx, request.GetScheduleToStartTimeout())
	ctx, cancel := workflow.WithCancel(ctx)
	childFuture := workflow.ExecuteChildWorkflow(ctx, workflowName, request.GetArgs())

	// Send ACK: Commented out because its no longer needed.
	// op := sendFutureACK(contextID, requestID, clientID)
	// <-op.GetChannel()

	// create the new ChildContext
	// add the ChildWorkflowFuture and the cancel func to the
	// ChildContexts map in the parent workflow's entry
	// in the WorkflowContexts map
	cctx := proxyworkflow.NewChild(childFuture, cancel)
	childID := wectx.AddChild(wectx.NextChildID(), cctx)

	// get the child workflow execution
	childWE := new(workflow.Execution)
	err := childFuture.GetChildWorkflowExecution().Get(ctx, childWE)
	if err != nil {
		reply.Build(err)
	}

	reply.Build(nil, append(make([]interface{}, 0), childID, childWE))

	return reply
}

func handleWorkflowWaitForChildRequest(requestCtx context.Context, request *messages.WorkflowWaitForChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	childID := request.GetChildID()
	Logger.Debug("WorkflowWaitForChildRequest Received",
		zap.Int64("ChildId", childID),
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowWaitForChildReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	cctx := wectx.GetChild(childID)
	if cctx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// wait on the child workflow
	var result []byte
	if err := cctx.GetFuture().Get(ctx, &result); err != nil {
		var temporalError *internal.TemporalError
		if isCanceledErr(err) {
			temporalError = internal.NewTemporalError(err, internal.CancelledError)
		} else {
			temporalError = internal.NewTemporalError(err)
		}

		reply.Build(temporalError)

		return reply
	}

	reply.Build(nil, result)

	defer wectx.RemoveChild(childID)

	return reply
}

func handleWorkflowSignalChildRequest(requestCtx context.Context, request *messages.WorkflowSignalChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	childID := request.GetChildID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	signalName := *request.GetSignalName()
	Logger.Debug("WorkflowSignalChildRequest Received",
		zap.String("Signal", signalName),
		zap.Int64("ChildId", childID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSignalChildReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	cctx := wectx.GetChild(childID)
	if cctx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// signal the child workflow
	future := cctx.GetFuture().SignalChildWorkflow(
		ctx,
		signalName,
		request.GetSignalArgs())

	// Send ACK: Commented out because its no longer needed.
	// op := sendFutureACK(contextID, requestID, clientID)
	// <-op.GetChannel()

	// wait on the future
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleWorkflowCancelChildRequest(requestCtx context.Context, request *messages.WorkflowCancelChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	childID := request.GetChildID()
	Logger.Debug("WorkflowCancelChildRequest Received",
		zap.Int64("ChildId", childID),
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowCancelChildReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	cctx := wectx.GetChild(childID)
	if cctx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// set replaying
	setReplayStatus(wectx.GetContext(), reply)

	// get cancel function
	// call the cancel function
	cancel := cctx.GetCancelFunction()
	cancel()

	reply.Build(nil)

	return reply
}

func handleWorkflowSetQueryHandlerRequest(requestCtx context.Context, request *messages.WorkflowSetQueryHandlerRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	queryName := *request.GetQueryName()
	Logger.Debug("WorkflowSetQueryHandlerRequest Received",
		zap.String("QueryName", queryName),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowSetQueryHandlerReply
	reply := messages.CreateReplyMessage(request)

	// get the workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// define the handler function
	ctx := wectx.GetContext()
	queryHandler := func(queryArgs []byte) ([]byte, error) {
		requestID := NextRequestID()
		Logger.Debug("Workflow Queried",
			zap.String("Query", queryName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		workflowQueryInvokeRequest := messages.NewWorkflowQueryInvokeRequest()
		workflowQueryInvokeRequest.SetRequestID(requestID)
		workflowQueryInvokeRequest.SetContextID(contextID)
		workflowQueryInvokeRequest.SetQueryArgs(queryArgs)
		workflowQueryInvokeRequest.SetQueryName(&queryName)
		workflowQueryInvokeRequest.SetClientID(clientID)

		// set ReplayStatus
		setReplayStatus(ctx, workflowQueryInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowQueryInvokeRequest)
		op.SetContextID(contextID)
		op.SetChannel(make(chan interface{}))
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(workflowQueryInvokeRequest)

		Logger.Debug("WorkflowQueryInvoke sent",
			zap.String("Query", queryName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Query Failed With Error",
				zap.String("Query", queryName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Query Completed Successfully",
				zap.String("Query", queryName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Query result unexpected",
				zap.String("Query", queryName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// Set the query handler with the
	// temporal server
	err := workflow.SetQueryHandler(ctx, queryName, queryHandler)
	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleWorkflowQueryRequest(requestCtx context.Context, request *messages.WorkflowQueryRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	clientID := request.GetClientID()
	queryName := *request.GetQueryName()
	Logger.Debug("WorkflowQueryRequest Received",
		zap.String("QueryName", queryName),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowQueryReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// query the workflow via the temporal client
	value, err := clientHelper.QueryWorkflow(
		ctx,
		workflowID,
		runID,
		*request.GetNamespace(),
		queryName,
		request.GetQueryArgs())

	if err != nil {
		reply.Build(err)
		return reply
	}

	// extract the result
	var result []byte
	if value.HasValue() {
		err = value.Get(&result)
		if err != nil {
			reply.Build(err)
			return reply
		}
	}

	reply.Build(nil, result)

	return reply
}

func handleWorkflowGetVersionRequest(requestCtx context.Context, request *messages.WorkflowGetVersionRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowGetVersionRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowGetVersionReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx := wectx.GetContext()

	// set ReplayStatus
	setReplayStatus(ctx, reply)

	// get the workflow version
	version := workflow.GetVersion(
		ctx,
		*request.GetChangeID(),
		workflow.Version(request.GetMinSupported()),
		workflow.Version(request.GetMaxSupported()))

	reply.Build(nil, version)

	return reply
}

func handleWorkflowQueueNewRequest(requestCtx context.Context, request *messages.WorkflowQueueNewRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	queueID := request.GetQueueID()
	Logger.Debug("WorkflowQueueNewRequest Received",
		zap.Int64("QueueId", queueID),
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowQueueNewReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx := wectx.GetContext()

	// set ReplayStatus
	setReplayStatus(ctx, reply)

	capacity := int(request.GetCapacity())
	queue := workflow.NewBufferedChannel(ctx, capacity)
	queueID = wectx.AddQueue(queueID, queue)

	Logger.Info("Queue successfully added",
		zap.Int64("QueueId", queueID),
		zap.Int("Capacity", capacity),
		zap.Int64("ContextId", contextID))

	reply.Build(nil)

	return reply
}

func handleWorkflowQueueWriteRequest(requestCtx context.Context, request *messages.WorkflowQueueWriteRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	queueID := request.GetQueueID()
	Logger.Debug("WorkflowQueueWriteRequest Received",
		zap.Int64("QueueId", queueID),
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowQueueWriteReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx := wectx.GetContext()

	// set ReplayStatus
	setReplayStatus(ctx, reply)

	data := request.GetData()
	queue := wectx.GetQueue(queueID)
	if queue == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// check if we should block and wait to enqueue
	// or just try to enqueue without blocking.
	if request.GetNoBlock() {
		s := workflow.NewSelector(ctx)

		// indicates that the value was successfully added to the queue
		// and is not full.
		s.AddSend(queue, data, func() {
			reply.Build(nil, false)
		})

		// indicates that the queue is full and the value was not added
		// to the queue.
		s.AddDefault(func() {
			reply.Build(nil, true)
		})
		s.Select(ctx)
	} else {
		// send data to queue
		queue.Send(ctx, data)
	}

	Logger.Info("Successfully Added to Queue",
		zap.Int64("QueueId", queueID),
		zap.Any("Data", data),
		zap.Int64("ContextId", contextID))

	reply.Build(nil)

	return reply
}

func handleWorkflowQueueReadRequest(requestCtx context.Context, request *messages.WorkflowQueueReadRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	queueID := request.GetQueueID()
	Logger.Debug("WorkflowQueueReadRequest Received",
		zap.Int64("QueueId", queueID),
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowQueueReadReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)

	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx := wectx.GetContext()

	// set ReplayStatus
	setReplayStatus(ctx, reply)

	queue := wectx.GetQueue(queueID)
	if queue == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// read value from queue
	var data []byte
	var isClosed bool
	var temporalError *internal.TemporalError
	timeout := request.GetTimeout()
	s := workflow.NewSelector(ctx)

	// check for timeout
	if timeout > time.Duration(0) {
		timer := workflow.NewTimer(ctx, timeout)
		s.AddFuture(timer, func(f workflow.Future) {
			isReady := false
			err := f.Get(ctx, &isReady)
			if err != nil {
				temporalError = internal.NewTemporalError(err, internal.CancelledError)
			} else {
				temporalError = internal.NewTemporalError(fmt.Errorf("Timeout reading from workflow queue: %d", queueID), internal.TimeoutError)
			}
		})
	}
	s.AddReceive(queue, func(c workflow.ReceiveChannel, more bool) {
		c.Receive(ctx, &data)
		if data == nil {
			isClosed = true
		}
	})
	s.Select(ctx)

	reply.Build(temporalError, append(make([]interface{}, 0), data, isClosed))

	return reply
}

func handleWorkflowQueueCloseRequest(requestCtx context.Context, request *messages.WorkflowQueueCloseRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	queueID := request.GetQueueID()
	Logger.Debug("WorkflowQueueCloseRequest Received",
		zap.Int64("QueueId", queueID),
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new WorkflowQueueCloseReply
	reply := messages.CreateReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx := wectx.GetContext()

	// set ReplayStatus
	setReplayStatus(ctx, reply)

	queue := wectx.GetQueue(queueID)
	if queue == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// close the queue
	queue.Close()

	Logger.Info("Successfully closed Queue",
		zap.Int64("QueueId", queueID),
		zap.Int64("ContextId", contextID))

	reply.Build(nil)

	return reply
}

// ----------------------------------------------------------------------
// IProxyRequest activity message type handler methods

func handleActivityRegisterRequest(requestCtx context.Context, request *messages.ActivityRegisterRequest) messages.IProxyReply {
	activityName := *request.GetName()
	clientID := request.GetClientID()
	workerID := request.GetWorkerID()
	Logger.Debug("ActivityRegisterRequest Received",
		zap.String("Activity", activityName),
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityRegisterReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// define the activity function
	activityFunc := func(ctx context.Context, input []byte) ([]byte, error) {
		requestID := NextRequestID()
		contextID := proxyactivity.NextContextID()
		Logger.Debug("Executing Activity",
			zap.String("Activity", activityName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ActivityContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// add the context to ActivityContexts
		actx := proxyactivity.NewActivityContext(ctx)
		actx.SetActivityName(&activityName)
		contextID = ActivityContexts.Add(contextID, actx)

		// Send a ActivityInvokeRequest to the Neon.Temporal Lib
		// temporal-client
		activityInvokeRequest := messages.NewActivityInvokeRequest()
		activityInvokeRequest.SetRequestID(requestID)
		activityInvokeRequest.SetArgs(input)
		activityInvokeRequest.SetContextID(contextID)
		activityInvokeRequest.SetActivity(&activityName)
		activityInvokeRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// get worker stop channel on the context
		// Send and wait for
		// ActivityStoppingRequest
		stopChan := activity.GetWorkerStopChannel(ctx)
		s := func() {
			<-stopChan
			requestID := NextRequestID()
			Logger.Debug("Stopping Activity",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Int("ProcessId", os.Getpid()))

			// send an ActivityStoppingRequest to the client
			activityStoppingRequest := messages.NewActivityStoppingRequest()
			activityStoppingRequest.SetRequestID(requestID)
			activityStoppingRequest.SetActivityID(&activityName)
			activityStoppingRequest.SetContextID(contextID)
			activityStoppingRequest.SetClientID(clientID)

			// create the Operation for this request and add it to the operations map
			stoppingReplyChan := make(chan interface{})
			op := NewOperation(requestID, activityStoppingRequest)
			op.SetChannel(stoppingReplyChan)
			op.SetContextID(contextID)
			Operations.Add(requestID, op)

			// send the request and wait for the reply
			go sendMessage(activityStoppingRequest)

			Logger.Debug("ActivityStoppingRequest sent",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Int("ProcessId", os.Getpid()))

			<-stoppingReplyChan
		}

		// run go routines
		go s()
		go sendMessage(activityInvokeRequest)

		Logger.Debug("ActivityInvokeRequest sent",
			zap.String("Activity", activityName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ActivityContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Activity Failed With Error",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Activity Completed Successfully",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Activity Result unexpected",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	clientHelper.ActivityRegister(workerID, activityFunc, activityName)
	Logger.Debug("Activity Successfully Registered", zap.String("ActivityName", activityName))

	reply.Build(nil)

	return reply
}

func handleActivityExecuteRequest(requestCtx context.Context, request *messages.ActivityExecuteRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityName := *request.GetActivity()
	Logger.Debug("ActivityExecuteRequest Received",
		zap.String("ActivityName", activityName),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityExecuteReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the activity options
	var opts workflow.ActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// get the activity options, the context,
	// and set the activity options on the context
	ctx := workflow.WithActivityOptions(wectx.GetContext(), opts)
	ctx = workflow.WithWorkflowNamespace(ctx, *request.GetNamespace())
	future := workflow.ExecuteActivity(ctx, activityName, request.GetArgs())

	// Send ACK: Commented out because its no longer needed.
	// op := sendFutureACK(contextID, requestID, clientID)
	// <-op.GetChannel()

	// execute the activity
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleActivityStartRequest(requestCtx context.Context, request *messages.ActivityStartRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	activity := *request.GetActivity()
	Logger.Debug("ActivityStartRequest Received",
		zap.String("Activity", activity),
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityStartReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the activity options
	var opts workflow.ActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// get the activity options, the context,
	// and set the activity options on the context
	// and set cancelation
	var cancel workflow.CancelFunc
	ctx := workflow.WithActivityOptions(wectx.GetContext(), opts)
	ctx = workflow.WithWorkflowNamespace(ctx, *request.GetNamespace())
	ctx, cancel = workflow.WithCancel(ctx)

	//execute workflow
	future := workflow.ExecuteActivity(ctx, activity, request.GetArgs())

	// add to workflow context map
	_ = wectx.AddActivity(activityID, *proxyworkflow.NewActivity(future, cancel))

	reply.Build(nil)

	return reply
}

func handleActivityGetResultRequest(requestCtx context.Context, request *messages.ActivityGetResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	Logger.Debug("ActivityGetResultRequest Received",
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetResultReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	activity := wectx.GetActivity(activityID)
	if activity.GetFuture() == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}
	defer wectx.RemoveActivity(activityID)

	// execute the activity
	var result []byte
	if err := activity.GetFuture().Get(wectx.GetContext(), &result); err != nil {
		reply.Build(err)
		return reply
	}
	reply.Build(nil, result)

	return reply
}

func handleActivityHasHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityHasHeartbeatDetailsRequest) messages.IProxyReply {
	Logger.Debug("ActivityHasHeartbeatDetailsRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityHasHeartbeatDetailsReply
	reply := messages.CreateReplyMessage(request)

	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	reply.Build(nil, activity.HasHeartbeatDetails(actx.GetContext()))

	return reply
}

func handleActivityGetHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityGetHeartbeatDetailsRequest) messages.IProxyReply {
	Logger.Debug("ActivityGetHeartbeatDetailsRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetHeartbeatDetailsReply
	reply := messages.CreateReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the activity heartbeat details
	var details []byte
	err := activity.GetHeartbeatDetails(actx.GetContext(), &details)
	if err != nil {
		reply.Build(err)
		return reply
	}
	reply.Build(nil, details)

	return reply
}

func handleActivityRecordHeartbeatRequest(requestCtx context.Context, request *messages.ActivityRecordHeartbeatRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	contextID := request.GetContextID()
	Logger.Debug("ActivityRecordHeartbeatRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityRecordHeartbeatReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check to see if external or internal
	// record heartbeat
	var err error
	details := request.GetDetails()
	if request.GetTaskToken() == nil {
		if request.GetActivityID() == nil {
			actx := ActivityContexts.Get(contextID)
			if actx == nil {
				reply.Build(internal.ErrEntityNotExist)
				return reply
			}

			activity.RecordHeartbeat(ActivityContexts.Get(contextID).GetContext(), details)

		} else {
			err = clientHelper.RecordActivityHeartbeatByID(
				ctx,
				*request.GetNamespace(),
				*request.GetWorkflowID(),
				*request.GetRunID(),
				*request.GetActivityID(),
				details)
		}

	} else {
		err = clientHelper.RecordActivityHeartbeat(
			ctx,
			request.GetTaskToken(),
			*request.GetNamespace(),
			details)
	}

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleActivityGetInfoRequest(requestCtx context.Context, request *messages.ActivityGetInfoRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("ActivityGetInfoRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetInfoReply
	reply := messages.CreateReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(contextID)
	if actx == nil {
		reply.Build(internal.ErrConnection)
		return reply
	}

	// get info
	// build the reply
	info := activity.GetInfo(actx.GetContext())

	reply.Build(nil, &info)

	return reply
}

func handleActivityCompleteRequest(requestCtx context.Context, request *messages.ActivityCompleteRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	Logger.Debug("ActivityCompleteRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityCompleteReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check the task token
	// and complete activity
	var err error
	taskToken := request.GetTaskToken()

	if taskToken == nil {
		err = clientHelper.CompleteActivityByID(
			ctx,
			*request.GetNamespace(),
			*request.GetWorkflowID(),
			*request.GetRunID(),
			*request.GetActivityID(),
			request.GetResult(),
			request.GetError())

	} else {
		err = clientHelper.CompleteActivity(
			ctx,
			taskToken,
			*request.GetNamespace(),
			request.GetResult(),
			request.GetError())
	}

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleActivityExecuteLocalRequest(requestCtx context.Context, request *messages.ActivityExecuteLocalRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityTypeID := request.GetActivityTypeID()
	Logger.Debug("ActivityExecuteLocalRequest Received",
		zap.Int64("ActivityTypeId", activityTypeID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", requestID),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityExecuteLocalReply
	reply := messages.CreateReplyMessage(request)

	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// the local activity function
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {
		actx := proxyactivity.NewActivityContext(ctx)
		activityContextID := ActivityContexts.Add(proxyactivity.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Temporal Lib
		// temporal-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)
		activityInvokeLocalRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeLocalRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(activityContextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(activityInvokeLocalRequest)

		Logger.Debug("ActivityInvokeLocalRequest sent",
			zap.Int64("ActivityTypeId", activityTypeID),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("ActivityContextId", activityContextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Activity Failed With Error",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Activity Successful",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Activity Result Unexpected",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// get the activity options
	var opts workflow.LocalActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// set the activity options on the context
	// execute local activity
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)
	future := workflow.ExecuteLocalActivity(ctx, localActivityFunc, request.GetArgs())

	// Send ACK: Commented out because its no longer needed.
	// op := sendFutureACK(contextID, requestID, clientID)
	// <-op.GetChannel()

	// wait for the future to be unblocked
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleActivityStartLocalRequest(requestCtx context.Context, request *messages.ActivityStartLocalRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	activityTypeID := request.GetActivityTypeID()
	Logger.Debug("ActivityStartLocalRequest Received",
		zap.Int64("ActivityTypeId", activityTypeID),
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityStartLocalReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// the local activity function
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {
		actx := proxyactivity.NewActivityContext(ctx)
		activityContextID := ActivityContexts.Add(proxyactivity.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Temporal Lib
		// temporal-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)
		activityInvokeLocalRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeLocalRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(activityContextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(activityInvokeLocalRequest)

		Logger.Debug("ActivityInvokeLocalRequest sent",
			zap.Int64("ActivityTypeId", activityTypeID),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("ActivityContextId", activityContextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Activity Failed With Error",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Activity Successful",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Activity Result Unexpected",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	/// get the activity options
	var opts workflow.LocalActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	/// set the activity options on the context
	// execute local activity
	var cancel workflow.CancelFunc
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)
	ctx, cancel = workflow.WithCancel(ctx)
	future := workflow.ExecuteLocalActivity(ctx, localActivityFunc, request.GetArgs())

	// add to workflow context map
	_ = wectx.AddActivity(activityID, *proxyworkflow.NewActivity(future, cancel))
	reply.Build(nil)

	return reply
}

func handleActivityGetLocalResultRequest(requestCtx context.Context, request *messages.ActivityGetLocalResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	Logger.Debug("ActivityGetLocalResultRequest Received",
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetLocalResultReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	activity := wectx.GetActivity(activityID)
	if activity.GetFuture() == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}
	defer wectx.RemoveActivity(activityID)

	var result []byte
	if err := activity.GetFuture().Get(wectx.GetContext(), &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}
