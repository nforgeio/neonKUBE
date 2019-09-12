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

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/encoded"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/cadence-proxy/internal"
	proxyactivity "github.com/cadence-proxy/internal/cadence/activity"
	proxyclient "github.com/cadence-proxy/internal/cadence/client"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
	proxyworker "github.com/cadence-proxy/internal/cadence/worker"
	proxyworkflow "github.com/cadence-proxy/internal/cadence/workflow"
	"github.com/cadence-proxy/internal/messages"
)

// ----------------------------------------------------------------------
// IProxyRequest client message type handler methods

func handlePingRequest(requestCtx context.Context, request *messages.PingRequest) messages.IProxyReply {
	Logger.Debug("PingRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new PingReply
	reply := createReplyMessage(request)
	buildReply(reply, nil)

	return reply
}

func handleCancelRequest(requestCtx context.Context, request *messages.CancelRequest) messages.IProxyReply {
	targetID := request.GetTargetRequestID()
	Logger.Debug("CancelRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("TargetId", targetID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new InitializeReply
	reply := createReplyMessage(request)
	buildReply(reply, nil, true)

	return reply
}

func handleConnectRequest(requestCtx context.Context, request *messages.ConnectRequest) messages.IProxyReply {
	Logger.Debug("ConnectRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ConnectReply
	reply := createReplyMessage(request)

	// get default domain and client options
	defaultDomain := *request.GetDomain()
	opts := client.Options{
		Identity: *request.GetIdentity(),
	}

	// create and set the logger
	logger := SetLogger(
		internal.LogLevel,
		internal.Debug,
		internal.LogToFile)
	clientHelper := proxyclient.NewClientHelper()
	clientHelper.Logger = logger.Named(internal.ProxyLoggerName)

	// configure the ClientHelper
	// setup the domain, service, and workflow clients
	err := clientHelper.SetupCadenceClients(requestCtx,
		*request.GetEndpoints(),
		defaultDomain,
		request.GetRetries(),
		request.GetRetryDelay(),
		&opts,
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrConnection))
		return reply
	}

	// set the timeout
	clientHelper.SetClientTimeout(request.GetClientTimeout())

	// reset the deadline on ctx with new timeout
	// and check if we need to register the default
	// domain
	if request.GetCreateDomain() {
		ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
		defer cancel()

		// register the domain
		retention := int32(365)
		err = clientHelper.RegisterDomain(ctx,
			&cadenceshared.RegisterDomainRequest{
				Name:                                   &defaultDomain,
				WorkflowExecutionRetentionPeriodInDays: &retention,
			},
		)
		if err != nil {
			if _, ok := err.(*cadenceshared.DomainAlreadyExistsError); !ok {
				Logger.Error("failed to register domain",
					zap.String("Domain Name", defaultDomain),
					zap.Error(err),
				)
				buildReply(reply, proxyerror.NewCadenceError(err))

				return reply
			}
		}
	}

	_ = Clients.Add(request.GetClientID(), clientHelper)
	buildReply(reply, nil)

	return reply
}

func handleDisconnectRequest(requestCtx context.Context, request *messages.DisconnectRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	Logger.Debug("DisconnectRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new DisconnectReply
	reply := createReplyMessage(request)

	// destroy the client
	if err := Clients.Get(clientID).DestroyClient(); err != nil {
		Logger.Error("Could not disconnect cadence client.",
			zap.Int64("ClientID", clientID),
			zap.Error(err),
		)
		buildReply(reply, proxyerror.NewCadenceError(err))

		return reply
	}

	// remove the client from Clients map
	// return reply
	_ = Clients.Remove(clientID)
	buildReply(reply, nil)

	Logger.Debug("Successfully removed client.",
		zap.Int64("ClientID", clientID),
	)

	return reply
}

func handleHeartbeatRequest(requestCtx context.Context, request *messages.HeartbeatRequest) messages.IProxyReply {
	Logger.Debug("HeartbeatRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new HeartbeatReply
	reply := createReplyMessage(request)
	buildReply(reply, nil)

	return reply
}

func handleInitializeRequest(requestCtx context.Context, request *messages.InitializeRequest) messages.IProxyReply {
	Logger.Debug("InitializeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new InitializeReply
	reply := createReplyMessage(request)

	// set the reply address
	if internal.DebugPrelaunched {
		replyAddress = "http://127.0.0.2:5001/"
	} else {
		address := *request.GetLibraryAddress()
		port := request.GetLibraryPort()
		replyAddress = fmt.Sprintf("http://%s:%d/",
			address,
			port,
		)
	}

	logLevel := request.GetLogLevel()
	internal.LogLevel = logLevel
	logger := SetLogger(
		internal.LogLevel,
		internal.Debug,
		internal.LogToFile)
	Logger = logger.Named(internal.ProxyLoggerName)

	Logger.Debug("Initialization info",
		zap.String("Reply Address", replyAddress),
		zap.String("LogLevel", logLevel.String()),
	)
	buildReply(reply, nil)

	return reply
}

func handleTerminateRequest(requestCtx context.Context, request *messages.TerminateRequest) messages.IProxyReply {
	Logger.Debug("TerminateRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new TerminateReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true
	buildReply(reply, nil)

	return reply
}

func handleNewWorkerRequest(requestCtx context.Context, request *messages.NewWorkerRequest) messages.IProxyReply {
	domain := *request.GetDomain()
	taskList := *request.GetTaskList()
	Logger.Debug("NewWorkerRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.String("TaskList", taskList),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new NewWorkerReply
	reply := createReplyMessage(request)

	// configure cadence logger
	logger := SetLogger(
		internal.LogLevel,
		internal.Debug,
		internal.LogToFile)

	// get options
	opts := request.GetOptions()
	opts.Logger = logger.Named(internal.CadenceLoggerName)

	// create a new worker using a configured ClientHelper instance
	workerID := proxyworker.NextWorkerID()
	worker, err := Clients.Get(request.GetClientID()).StartWorker(
		domain,
		taskList,
		*opts,
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err), workerID)
		return reply
	}

	// put the worker and workerID from the new worker to the
	workerID = Workers.Add(workerID, worker)
	Logger.Debug("New Worker Created",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("WorkerId", workerID),
		zap.String("Domain", domain),
		zap.String("TaskList", taskList),
		zap.Int("ProcessId", os.Getpid()),
	)
	buildReply(reply, nil, workerID)

	return reply
}

func handleStopWorkerRequest(requestCtx context.Context, request *messages.StopWorkerRequest) messages.IProxyReply {
	workerID := request.GetWorkerID()
	Logger.Debug("StopWorkerRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("WorkerId", workerID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new StopWorkerReply
	reply := createReplyMessage(request)

	// get the workerID from the request so that we know
	// what worker to stop
	worker := Workers.Get(workerID)
	if worker == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// stop the worker and
	// remove it from the Workers map
	Clients.Get(request.GetClientID()).StopWorker(worker)
	workerID = Workers.Remove(workerID)

	Logger.Debug("Worker has been removed from Workers", zap.Int64("WorkerID", workerID))
	buildReply(reply, nil)

	return reply
}

func handleDomainDescribeRequest(requestCtx context.Context, request *messages.DomainDescribeRequest) messages.IProxyReply {
	domain := *request.GetName()
	Logger.Debug("DomainDescribeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new DomainDescribeReply
	reply := createReplyMessage(request)

	// create context with timeout
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// send a describe domain request to the cadence server
	describeDomainResponse, err := clientHelper.DescribeDomain(ctx, domain)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, describeDomainResponse)

	return reply
}

func handleDomainRegisterRequest(requestCtx context.Context, request *messages.DomainRegisterRequest) messages.IProxyReply {
	Logger.Debug("DomainRegisterRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new DomainRegisterReply
	reply := createReplyMessage(request)

	// create a new cadence domain RegisterDomainRequest for
	// registering a new domain
	emitMetrics := request.GetEmitMetrics()
	retentionDays := request.GetRetentionDays()
	domainName := request.GetName()
	registerDomainRequest := cadenceshared.RegisterDomainRequest{
		Name:                                   domainName,
		Description:                            request.GetDescription(),
		OwnerEmail:                             request.GetOwnerEmail(),
		EmitMetric:                             &emitMetrics,
		WorkflowExecutionRetentionPeriodInDays: &retentionDays,
	}

	// create context with timeout
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// register the domain using the RegisterDomainRequest
	err := clientHelper.RegisterDomain(ctx, &registerDomainRequest)
	if err != nil {
		Logger.Error("failed to register domain",
			zap.String("Domain Name", *domainName),
			zap.Error(err),
		)
		buildReply(reply, proxyerror.NewCadenceError(err))

		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleDomainUpdateRequest(requestCtx context.Context, request *messages.DomainUpdateRequest) messages.IProxyReply {
	domain := *request.GetName()
	Logger.Debug("DomainUpdateRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new DomainUpdateReply
	reply := createReplyMessage(request)

	// DomainUpdateRequest.Configuration
	configurationEmitMetrics := request.GetConfigurationEmitMetrics()
	configurationRetentionDays := request.GetConfigurationRetentionDays()
	configuration := cadenceshared.DomainConfiguration{
		EmitMetric:                             &configurationEmitMetrics,
		WorkflowExecutionRetentionPeriodInDays: &configurationRetentionDays,
	}

	// DomainUpdateRequest.UpdatedInfo
	updatedInfo := cadenceshared.UpdateDomainInfo{
		Description: request.GetUpdatedInfoDescription(),
		OwnerEmail:  request.GetUpdatedInfoOwnerEmail(),
	}

	// DomainUpdateRequest
	domainUpdateRequest := cadenceshared.UpdateDomainRequest{
		Name:          &domain,
		Configuration: &configuration,
		UpdatedInfo:   &updatedInfo,
	}

	// create context with timeout
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// Update the domain using the UpdateDomainRequest
	err := clientHelper.UpdateDomain(ctx, &domainUpdateRequest)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

// ----------------------------------------------------------------------
// IProxyRequest workflow message type handler methods

func handleWorkflowRegisterRequest(requestCtx context.Context, request *messages.WorkflowRegisterRequest) messages.IProxyReply {
	workflowName := request.GetName()
	Logger.Debug("WorkflowRegisterRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ClientId", request.GetClientID()),
		zap.String("Workflow", *workflowName),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowRegisterReply
	reply := createReplyMessage(request)

	// create workflow function
	workflowFunc := func(ctx workflow.Context, clientID int64, input []byte) ([]byte, error) {
		contextID := proxyworkflow.NextContextID()
		requestID := NextRequestID()
		Logger.Debug("Executing Workflow",
			zap.String("Workflow", *workflowName),
			zap.Int64("ClientId", clientID),
			zap.Int64("RequestId", requestID),
			zap.Int64("ContextId", contextID),
			zap.Int("ProcessId", os.Getpid()),
		)

		// set the WorkflowContext in WorkflowContexts
		wectx := proxyworkflow.NewWorkflowContext(ctx)
		wectx.SetWorkflowName(workflowName)
		contextID = WorkflowContexts.Add(contextID, wectx)

		// Send a WorkflowInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		workflowInvokeRequest := messages.NewWorkflowInvokeRequest()
		workflowInvokeRequest.SetRequestID(requestID)
		workflowInvokeRequest.SetContextID(contextID)
		workflowInvokeRequest.SetArgs(input)
		workflowInvokeRequest.SetClientID(clientID)

		// get the WorkflowInfo (Domain, WorkflowID, RunID, WorkflowType,
		// TaskList, ExecutionStartToCloseTimeout)
		// from the context
		workflowInfo := workflow.GetInfo(ctx)
		workflowInvokeRequest.SetDomain(&workflowInfo.Domain)
		workflowInvokeRequest.SetWorkflowID(&workflowInfo.WorkflowExecution.ID)
		workflowInvokeRequest.SetRunID(&workflowInfo.WorkflowExecution.RunID)
		workflowInvokeRequest.SetWorkflowType(&workflowInfo.WorkflowType.Name)
		workflowInvokeRequest.SetTaskList(&workflowInfo.TaskListName)
		workflowInvokeRequest.SetExecutionStartToCloseTimeout(time.Duration(int64(workflowInfo.ExecutionStartToCloseTimeoutSeconds) * int64(time.Second)))

		// set ReplayStatus
		setReplayStatus(ctx, workflowInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := messages.NewOperation(requestID, workflowInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send workflowInvokeRequest
		go sendMessage(workflowInvokeRequest)

		Logger.Debug("WorkflowInvokeRequest sent",
			zap.String("Workflow", *workflowName),
			zap.Int64("RequestId", requestID),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int("ProcessId", os.Getpid()),
		)

		// block and get result
		result := <-op.GetChannel()
		switch s := result.(type) {

		// workflow failed
		case error:
			if isForceReplayErr(s) {
				panic("force-replay")
			}

			Logger.Error("Workflow Failed With Error",
				zap.String("Workflow", *workflowName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return nil, s

		// workflow succeeded
		case []byte:
			Logger.Debug("Workflow Completed Successfully",
				zap.String("Workflow", *workflowName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("Unexpected result type %v.  result must be an error or []byte.", reflect.TypeOf(s))
		}
	}

	// register the workflow
	workflowRegisterWithOptions(workflowFunc, workflow.RegisterOptions{Name: *workflowName})
	Logger.Debug("workflow successfully registered", zap.String("WorkflowName", *workflowName))
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteRequest(requestCtx context.Context, request *messages.WorkflowExecuteRequest) messages.IProxyReply {
	workflowName := *request.GetWorkflow()
	domain := *request.GetDomain()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowExecuteRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowName", workflowName),
		zap.String("Domain", domain),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowExecuteReply
	reply := createReplyMessage(request)

	// create the context
	clientHelper := Clients.Get(clientID)
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
		domain,
		opts,
		workflowName,
		clientID,
		request.GetArgs())
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}

	// extract the workflow ID and RunID
	workflowExecution := workflow.Execution{
		ID:    workflowRun.GetID(),
		RunID: workflowRun.GetRunID(),
	}
	buildReply(reply, nil, &workflowExecution)

	return reply
}

func handleWorkflowCancelRequest(requestCtx context.Context, request *messages.WorkflowCancelRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	Logger.Debug("WorkflowCancelRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowCancelReply
	reply := createReplyMessage(request)

	// create the context to cancel the workflow
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// cancel the specified workflow
	err := clientHelper.CancelWorkflow(ctx,
		workflowID,
		runID,
		*request.GetDomain(),
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleWorkflowTerminateRequest(requestCtx context.Context, request *messages.WorkflowTerminateRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	Logger.Debug("WorkflowTerminateRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowTerminateReply
	reply := createReplyMessage(request)

	// create the context to terminate the workflow
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// terminate the specified workflow
	err := clientHelper.TerminateWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetDomain(),
		*request.GetReason(),
		request.GetDetails(),
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSignalWithStartRequest(requestCtx context.Context, request *messages.WorkflowSignalWithStartRequest) messages.IProxyReply {
	workflow := *request.GetWorkflow()
	workflowID := *request.GetWorkflowID()
	Logger.Debug("WorkflowSignalWithStartRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Workflow", workflow),
		zap.String("WorkflowId", workflowID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSignalWithStartReply
	reply := createReplyMessage(request)

	// create the context
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// signalwithstart the specified workflow
	workflowExecution, err := clientHelper.SignalWithStartWorkflow(ctx,
		workflowID,
		*request.GetDomain(),
		*request.GetSignalName(),
		request.GetSignalArgs(),
		*request.GetOptions(),
		workflow,
		request.GetWorkflowArgs(),
	)

	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, workflowExecution)

	return reply
}

func handleWorkflowSetCacheSizeRequest(requestCtx context.Context, request *messages.WorkflowSetCacheSizeRequest) messages.IProxyReply {
	Logger.Debug("WorkflowSetCacheSizeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSetCacheSizeReply
	reply := createReplyMessage(request)

	// set the sticky workflow cache size
	worker.SetStickyWorkflowCacheSize(request.GetSize())
	buildReply(reply, nil)

	return reply
}

func handleWorkflowMutableRequest(requestCtx context.Context, request *messages.WorkflowMutableRequest) messages.IProxyReply {
	Logger.Debug("WorkflowMutableRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowMutableReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
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
		if v, ok := a.(*proxyerror.CadenceError); ok {
			if _v, _ok := b.(*proxyerror.CadenceError); _ok {
				if v.GetType() == _v.GetType() &&
					v.ToString() == _v.ToString() {
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
		value = workflow.MutableSideEffect(ctx,
			*mutableID,
			mutableFunc,
			equals,
		)

	} else {
		value = workflow.SideEffect(ctx, mutableFunc)
	}

	// extract the result
	var result []byte
	err := value.Get(&result)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDescribeExecutionRequest(requestCtx context.Context, request *messages.WorkflowDescribeExecutionRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	Logger.Debug("WorkflowDescribeExecutionRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowDescribeExecutionReply
	reply := createReplyMessage(request)

	// create the context
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// DescribeWorkflow call to cadence client
	dwer, err := clientHelper.DescribeWorkflowExecution(ctx,
		workflowID,
		runID,
		*request.GetDomain(),
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, dwer)

	return reply
}

func handleWorkflowGetResultRequest(requestCtx context.Context, request *messages.WorkflowGetResultRequest) messages.IProxyReply {
	Logger.Debug("WorkflowGetResultRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowGetResultReply
	reply := createReplyMessage(request)

	// create the context
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// call GetWorkflow
	workflowRun, err := clientHelper.GetWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetDomain(),
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}

	// get the result of WorkflowRun
	var result []byte
	err = workflowRun.Get(ctx, &result)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowSignalSubscribeRequest(requestCtx context.Context, request *messages.WorkflowSignalSubscribeRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowSignalSubscribeRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", request.GetContextID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSignalSubscribeReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// placeholder to receive signal args from the
	// signal upon receive
	var signalArgs []byte
	signalName := request.GetSignalName()

	// create a selector, add a receiver and wait for the signal on
	// the channel
	ctx := wectx.GetContext()
	selector := workflow.NewSelector(ctx)
	selector = selector.AddReceive(workflow.GetSignalChannel(ctx, *signalName), func(channel workflow.Channel, more bool) {
		channel.Receive(ctx, &signalArgs)
		Logger.Debug("Received signal!", zap.String("signal", *signalName),
			zap.ByteString("args", signalArgs))

		// create the WorkflowSignalInvokeRequest
		requestID := NextRequestID()
		workflowSignalInvokeRequest := messages.NewWorkflowSignalInvokeRequest()
		workflowSignalInvokeRequest.SetRequestID(requestID)
		workflowSignalInvokeRequest.SetContextID(contextID)
		workflowSignalInvokeRequest.SetSignalArgs(signalArgs)
		workflowSignalInvokeRequest.SetSignalName(signalName)
		workflowSignalInvokeRequest.SetClientID(clientID)

		// set ReplayStatus
		setReplayStatus(ctx, workflowSignalInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := messages.NewOperation(requestID, workflowSignalInvokeRequest)
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
				zap.String("Signal", *signalName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Error(s),
			)

		case bool:
			Logger.Info("signal completed successfully",
				zap.String("Signal", *signalName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Bool("Success", s),
			)

		default:
			Logger.Info("signal result unexpected",
				zap.String("Signal", *signalName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Any("Result", s),
			)
		}
	})

	// Subscribe to named signal
	workflow.Go(ctx, func(ctx workflow.Context) {
		var err error
		var done bool
		selector = selector.AddReceive(ctx.Done(), func(c workflow.Channel, more bool) {
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
	Logger.Debug("WorkflowSignalRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSignalReply
	reply := createReplyMessage(request)

	// create the context to signal the workflow
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// signal the specified workflow
	err := clientHelper.SignalWorkflow(ctx,
		workflowID,
		runID,
		*request.GetDomain(),
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleWorkflowHasLastResultRequest(requestCtx context.Context, request *messages.WorkflowHasLastResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowHasLastResultRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowHasLastResultReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)
	buildReply(reply, nil, workflow.HasLastCompletionResult(ctx))

	return reply
}

func handleWorkflowGetLastResultRequest(requestCtx context.Context, request *messages.WorkflowGetLastResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowGetLastResultRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowGetLastResultReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set replay status
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// get the last completion result from the cadence client
	var result []byte
	err := workflow.GetLastCompletionResult(ctx, &result)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDisconnectContextRequest(requestCtx context.Context, request *messages.WorkflowDisconnectContextRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowDisconnectContextRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowDisconnectContextReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// create a new disconnected context
	// and then replace the existing one with the new one
	disconnectedCtx, cancel := workflow.NewDisconnectedContext(wectx.GetContext())
	wectx.SetContext(disconnectedCtx)
	wectx.SetCancelFunction(cancel)
	buildReply(reply, nil)

	return reply
}

func handleWorkflowGetTimeRequest(requestCtx context.Context, request *messages.WorkflowGetTimeRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowGetTimeRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowGetTimeReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set replay status
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)
	buildReply(reply, nil, workflow.Now(ctx))

	return reply
}

func handleWorkflowSleepRequest(requestCtx context.Context, request *messages.WorkflowSleepRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowSleepRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSleepReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// pause the current workflow for the specified duration
	var result interface{}
	future := workflow.NewTimer(ctx, request.GetDuration())

	// Send ACK
	op := sendFutureACK(contextID, request.GetRequestID(), request.GetClientID())
	<-op.GetChannel()

	// wait for the future to be unblocked
	err := future.Get(ctx, &result)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err, proxyerror.Cancelled))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteChildRequest(requestCtx context.Context, request *messages.WorkflowExecuteChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	Logger.Debug("WorkflowExecuteChildRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowExecuteChildReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
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
	childFuture := workflow.ExecuteChildWorkflow(ctx,
		*request.GetWorkflow(),
		clientID,
		request.GetArgs(),
	)

	// Send ACK
	op := sendFutureACK(contextID, requestID, clientID)
	<-op.GetChannel()

	// create the new ChildContext
	// add the ChildWorkflowFuture and the cancel func to the
	// ChildContexts map in the parent workflow's entry
	// in the WorkflowContexts map
	cctx := proxyworkflow.NewChildContext(childFuture, cancel)
	childID := wectx.AddChildContext(proxyworkflow.NextChildID(), cctx)

	// get the child workflow execution
	childWE := new(workflow.Execution)
	err := childFuture.GetChildWorkflowExecution().Get(ctx, childWE)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
	}
	buildReply(reply, nil, append(make([]interface{}, 0), childID, childWE))

	return reply
}

func handleWorkflowWaitForChildRequest(requestCtx context.Context, request *messages.WorkflowWaitForChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	childID := request.GetChildID()
	Logger.Debug("WorkflowWaitForChildRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ChildId", childID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowWaitForChildReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// wait on the child workflow
	var result []byte
	if err := cctx.GetFuture().Get(ctx, &result); err != nil {
		var cadenceError *proxyerror.CadenceError
		if isCanceledErr(err) {
			cadenceError = proxyerror.NewCadenceError(err, proxyerror.Cancelled)
		} else {
			cadenceError = proxyerror.NewCadenceError(err)
		}
		buildReply(reply, cadenceError)

		return reply
	}
	buildReply(reply, nil, result)

	// remove the child context
	defer func() {
		_ = wectx.RemoveChildContext(childID)
	}()

	return reply
}

func handleWorkflowSignalChildRequest(requestCtx context.Context, request *messages.WorkflowSignalChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	childID := request.GetChildID()
	Logger.Debug("WorkflowSignalChildRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ChildId", childID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSignalChildReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// signal the child workflow
	future := cctx.GetFuture().SignalChildWorkflow(ctx,
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	// Send ACK
	op := sendFutureACK(contextID, request.GetRequestID(), request.GetClientID())
	<-op.GetChannel()

	// wait on the future
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowCancelChildRequest(requestCtx context.Context, request *messages.WorkflowCancelChildRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	childID := request.GetChildID()
	Logger.Debug("WorkflowCancelChildRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ChildId", childID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowCancelChildReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))

		return reply
	}

	// set replaying
	setReplayStatus(wectx.GetContext(), reply)

	// get cancel function
	// call the cancel function
	cancel := cctx.GetCancelFunction()
	cancel()
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSetQueryHandlerRequest(requestCtx context.Context, request *messages.WorkflowSetQueryHandlerRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	Logger.Debug("WorkflowSetQueryHandlerRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowSetQueryHandlerReply
	reply := createReplyMessage(request)

	// get the workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// define the handler function
	ctx := wectx.GetContext()
	queryName := request.GetQueryName()
	queryHandler := func(queryArgs []byte) ([]byte, error) {

		// create the WorkflowSignalInvokeRequest
		requestID := NextRequestID()
		workflowQueryInvokeRequest := messages.NewWorkflowQueryInvokeRequest()
		workflowQueryInvokeRequest.SetRequestID(requestID)
		workflowQueryInvokeRequest.SetContextID(contextID)
		workflowQueryInvokeRequest.SetQueryArgs(queryArgs)
		workflowQueryInvokeRequest.SetQueryName(queryName)
		workflowQueryInvokeRequest.SetClientID(clientID)

		// set ReplayStatus
		setReplayStatus(ctx, workflowQueryInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := messages.NewOperation(requestID, workflowQueryInvokeRequest)
		op.SetContextID(contextID)
		op.SetChannel(make(chan interface{}))
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(workflowQueryInvokeRequest)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {

		// failure
		case error:
			Logger.Error("Query Failed With Error",
				zap.String("Query", *queryName),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return nil, s

		// success
		case []byte:
			Logger.Debug("Query Completed Successfully",
				zap.String("Query", *queryName),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("Unexpected result type %v.  result must be an error or []byte.", reflect.TypeOf(s))
		}
	}

	// Set the query handler with the
	// cadence server
	err := workflow.SetQueryHandler(ctx, *queryName, queryHandler)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleWorkflowQueryRequest(requestCtx context.Context, request *messages.WorkflowQueryRequest) messages.IProxyReply {
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	Logger.Debug("WorkflowQueryRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowQueryReply
	reply := createReplyMessage(request)

	// create the context
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// query the workflow via the cadence client
	value, err := clientHelper.QueryWorkflow(ctx,
		workflowID,
		runID,
		*request.GetDomain(),
		*request.GetQueryName(),
		request.GetQueryArgs(),
	)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}

	// extract the result
	var result []byte
	if value.HasValue() {
		err = value.Get(&result)
		if err != nil {
			buildReply(reply, proxyerror.NewCadenceError(err))
			return reply
		}
	}
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowGetVersionRequest(requestCtx context.Context, request *messages.WorkflowGetVersionRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("WorkflowGetVersionRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new WorkflowGetVersionReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// get the workflow version
	version := workflow.GetVersion(ctx,
		*request.GetChangeID(),
		workflow.Version(request.GetMinSupported()),
		workflow.Version(request.GetMaxSupported()),
	)
	buildReply(reply, nil, version)

	return reply
}

// ----------------------------------------------------------------------
// IProxyRequest activity message type handler methods

func handleActivityRegisterRequest(requestCtx context.Context, request *messages.ActivityRegisterRequest) messages.IProxyReply {
	activityName := request.GetName()
	Logger.Debug("ActivityRegisterRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Activity", *activityName),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityRegisterReply
	reply := createReplyMessage(request)

	// define the activity function
	activityFunc := func(ctx context.Context, clientID int64, input []byte) ([]byte, error) {
		requestID := NextRequestID()
		contextID := proxyactivity.NextContextID()
		Logger.Debug("Executing Activity",
			zap.String("Activity", *activityName),
			zap.Int64("ClientId", clientID),
			zap.Int64("RequestId", requestID),
			zap.Int64("ActivityContextId", contextID),
			zap.Int("ProcessId", os.Getpid()),
		)

		// add the context to ActivityContexts
		actx := proxyactivity.NewActivityContext(ctx)
		actx.SetActivityName(activityName)
		contextID = ActivityContexts.Add(contextID, actx)

		// Send a ActivityInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		activityInvokeRequest := messages.NewActivityInvokeRequest()
		activityInvokeRequest.SetRequestID(requestID)
		activityInvokeRequest.SetArgs(input)
		activityInvokeRequest.SetContextID(contextID)
		activityInvokeRequest.SetActivity(activityName)
		activityInvokeRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := messages.NewOperation(requestID, activityInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// get worker stop channel on the context
		stopChan := activity.GetWorkerStopChannel(ctx)

		// Send and wait for
		// ActivityStoppingRequest
		s := func() {

			// wait on the channel to receive the stop signal
			<-stopChan

			// send an ActivityStoppingRequest to the client
			requestID := NextRequestID()
			activityStoppingRequest := messages.NewActivityStoppingRequest()
			activityStoppingRequest.SetRequestID(requestID)
			activityStoppingRequest.SetActivityID(activityName)
			activityStoppingRequest.SetContextID(contextID)
			activityStoppingRequest.SetClientID(clientID)

			// create the Operation for this request and add it to the operations map
			stoppingReplyChan := make(chan interface{})
			op := messages.NewOperation(requestID, activityStoppingRequest)
			op.SetChannel(stoppingReplyChan)
			op.SetContextID(contextID)
			Operations.Add(requestID, op)

			// send the request and wait for the reply
			go sendMessage(activityStoppingRequest)
			<-stoppingReplyChan
		}

		// run go routines
		go s()
		go sendMessage(activityInvokeRequest)

		Logger.Debug("ActivityInvokeRequest sent",
			zap.String("Activity", *activityName),
			zap.Int64("RequestId", requestID),
			zap.Int64("ClientId", clientID),
			zap.Int64("ActivityContextId", contextID),
			zap.Int("ProcessId", os.Getpid()),
		)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {

		// failure
		case error:
			Logger.Error("Activity Failed With Error",
				zap.String("Activity", *activityName),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return nil, s

		// success
		case []byte:
			Logger.Debug("Activity Completed Successfully",
				zap.String("Activity", *activityName),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("Unexpected result type %v.  result must be an error or []byte.", reflect.TypeOf(s))
		}
	}

	// register the activity
	activityRegisterWithOptions(activityFunc, activity.RegisterOptions{Name: *activityName})
	Logger.Debug("Activity Successfully Registered", zap.String("ActivityName", *activityName))
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteRequest(requestCtx context.Context, request *messages.ActivityExecuteRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	Logger.Debug("ActivityExecuteRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.String("ActivityName", *request.GetActivity()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityExecuteReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// get the activity options, the context,
	// and set the activity options on the context
	opts := request.GetOptions()
	ctx := workflow.WithActivityOptions(wectx.GetContext(), *opts)
	ctx = workflow.WithWorkflowDomain(ctx, *request.GetDomain())
	ctx = workflow.WithScheduleToStartTimeout(ctx, request.GetScheduleToStartTimeout())
	future := workflow.ExecuteActivity(
		ctx,
		*request.GetActivity(),
		clientID,
		request.GetArgs())

	// Send ACK
	op := sendFutureACK(contextID, request.GetRequestID(), clientID)
	<-op.GetChannel()

	// execute the activity
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, result)

	return reply
}

func handleActivityHasHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityHasHeartbeatDetailsRequest) messages.IProxyReply {
	Logger.Debug("ActivityHasHeartbeatDetailsRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityHasHeartbeatDetailsReply
	reply := createReplyMessage(request)

	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}
	buildReply(reply, nil, activity.HasHeartbeatDetails(actx.GetContext()))

	return reply
}

func handleActivityGetHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityGetHeartbeatDetailsRequest) messages.IProxyReply {
	Logger.Debug("ActivityGetHeartbeatDetailsRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityGetHeartbeatDetailsReply
	reply := createReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// get the activity heartbeat details
	var details []byte
	err := activity.GetHeartbeatDetails(actx.GetContext(), &details)
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil, details)

	return reply
}

func handleActivityRecordHeartbeatRequest(requestCtx context.Context, request *messages.ActivityRecordHeartbeatRequest) messages.IProxyReply {
	Logger.Debug("ActivityRecordHeartbeatRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityRecordHeartbeatReply
	reply := createReplyMessage(request)

	var err error
	details := request.GetDetails()
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check to see if external or internal
	// record heartbeat
	if request.GetTaskToken() == nil {
		if request.GetActivityID() == nil {
			actx := ActivityContexts.Get(request.GetContextID())
			if actx == nil {
				buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
				return reply
			}
			activity.RecordHeartbeat(ActivityContexts.Get(request.GetContextID()).GetContext(), details)

		} else {
			err = clientHelper.RecordActivityHeartbeatByID(ctx,
				*request.GetDomain(),
				*request.GetWorkflowID(),
				*request.GetRunID(),
				*request.GetActivityID(),
				details,
			)
		}

	} else {
		err = clientHelper.RecordActivityHeartbeat(ctx,
			request.GetTaskToken(),
			*request.GetDomain(),
			details,
		)
	}
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleActivityGetInfoRequest(requestCtx context.Context, request *messages.ActivityGetInfoRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("ActivityGetInfoRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityGetInfoReply
	reply := createReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(contextID)
	if actx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrConnection))

		return reply
	}

	// get info
	// build the reply
	info := activity.GetInfo(actx.GetContext())
	buildReply(reply, nil, &info)

	return reply
}

func handleActivityCompleteRequest(requestCtx context.Context, request *messages.ActivityCompleteRequest) messages.IProxyReply {
	Logger.Debug("ActivityCompleteRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityCompleteReply
	reply := createReplyMessage(request)

	// create the context
	clientHelper := Clients.Get(request.GetClientID())
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check the task token
	// and complete activity
	var err error
	taskToken := request.GetTaskToken()
	if taskToken == nil {
		err = clientHelper.CompleteActivityByID(ctx,
			*request.GetDomain(),
			*request.GetWorkflowID(),
			*request.GetRunID(),
			*request.GetActivityID(),
			request.GetResult(),
			request.GetError(),
		)

	} else {
		err = clientHelper.CompleteActivity(ctx,
			taskToken,
			*request.GetDomain(),
			request.GetResult(),
			request.GetError(),
		)
	}
	if err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))
		return reply
	}
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteLocalRequest(requestCtx context.Context, request *messages.ActivityExecuteLocalRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	activityTypeID := request.GetActivityTypeID()
	clientID := request.GetClientID()
	Logger.Debug("ActivityExecuteLocalRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ActivityTypeId", activityTypeID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// new ActivityExecuteLocalReply
	reply := createReplyMessage(request)

	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, proxyerror.NewCadenceError(internal.ErrEntityNotExist))
		return reply
	}

	// the local activity function
	args := request.GetArgs()
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {

		// create an activity context entry in the ActivityContexts map
		// add the context to ActivityContexts
		actx := proxyactivity.NewActivityContext(ctx)
		activityContextID := ActivityContexts.Add(proxyactivity.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Cadence Lib
		// cadence-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)
		activityInvokeLocalRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := messages.NewOperation(requestID, activityInvokeLocalRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(activityContextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(activityInvokeLocalRequest)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {

		// failure
		case error:
			Logger.Error("Activity Failed With Error",
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return nil, s

		// success
		case []byte:
			Logger.Debug("Activity Successful",
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("Unexpected result type %v.  result must be an error or []byte.", reflect.TypeOf(s))
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
	future := workflow.ExecuteLocalActivity(ctx, localActivityFunc, args)

	// Send ACK
	op := sendFutureACK(contextID, request.GetRequestID(), request.GetClientID())
	<-op.GetChannel()

	// wait for the future to be unblocked
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		buildReply(reply, proxyerror.NewCadenceError(err))

		return reply
	}
	buildReply(reply, nil, result)

	return reply
}
