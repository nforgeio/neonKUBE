//-----------------------------------------------------------------------------
// FILE:		request_handler.go
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
	"net/http"
	"os"
	"reflect"
	"runtime/debug"
	"time"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/encoded"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	globals "github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/cadence/cadenceactivities"
	cadenceclient "github.com/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

// -------------------------------------------------------------------------
// IProxyRequest message type handler entrypoint

func handleIProxyRequest(request messages.IProxyRequest) error {

	// create a context for every request
	// defer panic recovery
	var err error
	var reply messages.IProxyReply
	ctx := context.Background()
	defer func() {

		// recover from panic
		if r := recover(); r != nil {
			reply = createReplyMessage(request)
			buildReply(reply, cadenceerrors.NewCadenceError(
				fmt.Errorf("recovered from panic when processing message type: %s, RequestId: %d\n%s",
					request.GetType().String(),
					request.GetRequestID(), string(debug.Stack()))),
			)
		}

		// send the reply
		var resp *http.Response
		resp, err = putToNeonCadenceClient(reply)
		if err != nil {
			return
		}
		err = resp.Body.Close()
		if err != nil {
			logger.Error("could not close response body", zap.Error(err))
		}
	}()

	// check for clientHelper
	if err := verifyClientHelper(request, clientHelper); err != nil {
		reply = createReplyMessage(request)
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return err
	}

	// handle the messages individually
	// based on their message type
	switch request.GetType() {

	// -------------------------------------------------------------------------
	// Client message types

	// InitializeRequest
	case messagetypes.InitializeRequest:
		if v, ok := request.(*messages.InitializeRequest); ok {
			reply = handleInitializeRequest(ctx, v)
		}

	// HeartbeatRequest
	case messagetypes.HeartbeatRequest:
		if v, ok := request.(*messages.HeartbeatRequest); ok {
			reply = handleHeartbeatRequest(ctx, v)
		}

	// CancelRequest
	case messagetypes.CancelRequest:
		if v, ok := request.(*messages.CancelRequest); ok {
			reply = handleCancelRequest(ctx, v)
		}

	// ConnectRequest
	case messagetypes.ConnectRequest:
		if v, ok := request.(*messages.ConnectRequest); ok {
			reply = handleConnectRequest(ctx, v)
		}

	// DomainDescribeRequest
	case messagetypes.DomainDescribeRequest:
		if v, ok := request.(*messages.DomainDescribeRequest); ok {
			reply = handleDomainDescribeRequest(ctx, v)
		}

	// DomainRegisterRequest
	case messagetypes.DomainRegisterRequest:
		if v, ok := request.(*messages.DomainRegisterRequest); ok {
			reply = handleDomainRegisterRequest(ctx, v)
		}

	// DomainUpdateRequest
	case messagetypes.DomainUpdateRequest:
		if v, ok := request.(*messages.DomainUpdateRequest); ok {
			reply = handleDomainUpdateRequest(ctx, v)
		}

	// TerminateRequest
	case messagetypes.TerminateRequest:
		if v, ok := request.(*messages.TerminateRequest); ok {
			reply = handleTerminateRequest(ctx, v)
		}

	// NewWorkerRequest
	case messagetypes.NewWorkerRequest:
		if v, ok := request.(*messages.NewWorkerRequest); ok {
			reply = handleNewWorkerRequest(ctx, v)
		}

	// StopWorkerRequest
	case messagetypes.StopWorkerRequest:
		if v, ok := request.(*messages.StopWorkerRequest); ok {
			reply = handleStopWorkerRequest(ctx, v)
		}

	// PingRequest
	case messagetypes.PingRequest:
		if v, ok := request.(*messages.PingRequest); ok {
			reply = handlePingRequest(ctx, v)
		}

	// -------------------------------------------------------------------------
	// Workflow message types

	// WorkflowRegisterRequest
	case messagetypes.WorkflowRegisterRequest:
		if v, ok := request.(*messages.WorkflowRegisterRequest); ok {
			reply = handleWorkflowRegisterRequest(ctx, v)
		}

	// WorkflowExecuteRequest
	case messagetypes.WorkflowExecuteRequest:
		if v, ok := request.(*messages.WorkflowExecuteRequest); ok {
			reply = handleWorkflowExecuteRequest(ctx, v)
		}

	// WorkflowCancelRequest
	case messagetypes.WorkflowCancelRequest:
		if v, ok := request.(*messages.WorkflowCancelRequest); ok {
			reply = handleWorkflowCancelRequest(ctx, v)
		}

	// WorkflowTerminateRequest
	case messagetypes.WorkflowTerminateRequest:
		if v, ok := request.(*messages.WorkflowTerminateRequest); ok {
			reply = handleWorkflowTerminateRequest(ctx, v)
		}

	// WorkflowSignalWithStartRequest
	case messagetypes.WorkflowSignalWithStartRequest:
		if v, ok := request.(*messages.WorkflowSignalWithStartRequest); ok {
			reply = handleWorkflowSignalWithStartRequest(ctx, v)
		}

	// WorkflowSetCacheSizeRequest
	case messagetypes.WorkflowSetCacheSizeRequest:
		if v, ok := request.(*messages.WorkflowSetCacheSizeRequest); ok {
			reply = handleWorkflowSetCacheSizeRequest(ctx, v)
		}

	// WorkflowQueryRequest
	case messagetypes.WorkflowQueryRequest:
		if v, ok := request.(*messages.WorkflowQueryRequest); ok {
			reply = handleWorkflowQueryRequest(ctx, v)
		}

	// WorkflowMutableRequest
	case messagetypes.WorkflowMutableRequest:
		if v, ok := request.(*messages.WorkflowMutableRequest); ok {
			reply = handleWorkflowMutableRequest(ctx, v)
		}

	// WorkflowDescribeExecutionRequest
	case messagetypes.WorkflowDescribeExecutionRequest:
		if v, ok := request.(*messages.WorkflowDescribeExecutionRequest); ok {
			reply = handleWorkflowDescribeExecutionRequest(ctx, v)
		}

	// WorkflowGetResultRequest
	case messagetypes.WorkflowGetResultRequest:
		if v, ok := request.(*messages.WorkflowGetResultRequest); ok {
			reply = handleWorkflowGetResultRequest(ctx, v)
		}

	// WorkflowSignalSubscribeRequest
	case messagetypes.WorkflowSignalSubscribeRequest:
		if v, ok := request.(*messages.WorkflowSignalSubscribeRequest); ok {
			reply = handleWorkflowSignalSubscribeRequest(ctx, v)
		}

	// WorkflowSignalRequest
	case messagetypes.WorkflowSignalRequest:
		if v, ok := request.(*messages.WorkflowSignalRequest); ok {
			reply = handleWorkflowSignalRequest(ctx, v)
		}

	// WorkflowHasLastResultRequest
	case messagetypes.WorkflowHasLastResultRequest:
		if v, ok := request.(*messages.WorkflowHasLastResultRequest); ok {
			reply = handleWorkflowHasLastResultRequest(ctx, v)
		}

	// WorkflowGetLastResultRequest
	case messagetypes.WorkflowGetLastResultRequest:
		if v, ok := request.(*messages.WorkflowGetLastResultRequest); ok {
			reply = handleWorkflowGetLastResultRequest(ctx, v)
		}

	// WorkflowDisconnectContextRequest
	case messagetypes.WorkflowDisconnectContextRequest:
		if v, ok := request.(*messages.WorkflowDisconnectContextRequest); ok {
			reply = handleWorkflowDisconnectContextRequest(ctx, v)
		}

	// WorkflowGetTimeRequest
	case messagetypes.WorkflowGetTimeRequest:
		if v, ok := request.(*messages.WorkflowGetTimeRequest); ok {
			reply = handleWorkflowGetTimeRequest(ctx, v)
		}

	// WorkflowSleepRequest
	case messagetypes.WorkflowSleepRequest:
		if v, ok := request.(*messages.WorkflowSleepRequest); ok {
			reply = handleWorkflowSleepRequest(ctx, v)
		}

	// WorkflowExecuteChildRequest
	case messagetypes.WorkflowExecuteChildRequest:
		if v, ok := request.(*messages.WorkflowExecuteChildRequest); ok {
			reply = handleWorkflowExecuteChildRequest(ctx, v)
		}

	// WorkflowWaitForChildRequest
	case messagetypes.WorkflowWaitForChildRequest:
		if v, ok := request.(*messages.WorkflowWaitForChildRequest); ok {
			reply = handleWorkflowWaitForChildRequest(ctx, v)
		}

	// WorkflowSignalChildRequest
	case messagetypes.WorkflowSignalChildRequest:
		if v, ok := request.(*messages.WorkflowSignalChildRequest); ok {
			reply = handleWorkflowSignalChildRequest(ctx, v)
		}

	// WorkflowCancelChildRequest
	case messagetypes.WorkflowCancelChildRequest:
		if v, ok := request.(*messages.WorkflowCancelChildRequest); ok {
			reply = handleWorkflowCancelChildRequest(ctx, v)
		}

	// WorkflowSetQueryHandlerRequest
	case messagetypes.WorkflowSetQueryHandlerRequest:
		if v, ok := request.(*messages.WorkflowSetQueryHandlerRequest); ok {
			reply = handleWorkflowSetQueryHandlerRequest(ctx, v)
		}

	// WorkflowGetVersionRequest
	case messagetypes.WorkflowGetVersionRequest:
		if v, ok := request.(*messages.WorkflowGetVersionRequest); ok {
			reply = handleWorkflowGetVersionRequest(ctx, v)
		}

	// -------------------------------------------------------------------------
	// Activity message types

	// ActivityExecuteRequest
	case messagetypes.ActivityExecuteRequest:
		if v, ok := request.(*messages.ActivityExecuteRequest); ok {
			reply = handleActivityExecuteRequest(ctx, v)
		}

	// ActivityRegisterRequest
	case messagetypes.ActivityRegisterRequest:
		if v, ok := request.(*messages.ActivityRegisterRequest); ok {
			reply = handleActivityRegisterRequest(ctx, v)
		}

	// ActivityHasHeartbeatDetailsRequest
	case messagetypes.ActivityHasHeartbeatDetailsRequest:
		if v, ok := request.(*messages.ActivityHasHeartbeatDetailsRequest); ok {
			reply = handleActivityHasHeartbeatDetailsRequest(ctx, v)
		}

	// ActivityGetHeartbeatDetailsRequest
	case messagetypes.ActivityGetHeartbeatDetailsRequest:
		if v, ok := request.(*messages.ActivityGetHeartbeatDetailsRequest); ok {
			reply = handleActivityGetHeartbeatDetailsRequest(ctx, v)
		}

	// ActivityRecordHeartbeatRequest
	case messagetypes.ActivityRecordHeartbeatRequest:
		if v, ok := request.(*messages.ActivityRecordHeartbeatRequest); ok {
			reply = handleActivityRecordHeartbeatRequest(ctx, v)
		}

	// ActivityGetInfoRequest
	case messagetypes.ActivityGetInfoRequest:
		if v, ok := request.(*messages.ActivityGetInfoRequest); ok {
			reply = handleActivityGetInfoRequest(ctx, v)
		}

	// ActivityCompleteRequest
	case messagetypes.ActivityCompleteRequest:
		if v, ok := request.(*messages.ActivityCompleteRequest); ok {
			reply = handleActivityCompleteRequest(ctx, v)
		}

	// ActivityExecuteLocalRequest
	case messagetypes.ActivityExecuteLocalRequest:
		if v, ok := request.(*messages.ActivityExecuteLocalRequest); ok {
			reply = handleActivityExecuteLocalRequest(ctx, v)
		}

	// Undefined message type
	default:

		// $debug(jack.burns): DELETE THIS!
		err := fmt.Errorf("unhandled message type. could not complete type assertion for type %d", request.GetType())
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))

		// set the reply
		reply = messages.NewProxyReply()
		reply.SetRequestID(request.GetRequestID())
		reply.SetError(cadenceerrors.NewCadenceError(err, cadenceerrors.Custom))
	}

	return err
}

// -------------------------------------------------------------------------
// IProxyRequest client message type handler methods

func handlePingRequest(requestCtx context.Context, request *messages.PingRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("PingRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new PingReply
	reply := createReplyMessage(request)
	buildReply(reply, nil)

	return reply
}

func handleCancelRequest(requestCtx context.Context, request *messages.CancelRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	targetID := request.GetTargetRequestID()
	logger.Debug("CancelRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("TargetId", targetID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new InitializeReply
	reply := createReplyMessage(request)
	buildReply(reply, nil, true)

	return reply
}

func handleConnectRequest(requestCtx context.Context, request *messages.ConnectRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ConnectRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ConnectReply
	reply := createReplyMessage(request)

	// get default domain and client options
	defaultDomain := *request.GetDomain()
	opts := client.Options{
		Identity: *request.GetIdentity(),
	}

	// configure the ClientHelper
	// setup the domain, service, and workflow clients
	clientHelper = cadenceclient.NewClientHelper()
	err := clientHelper.SetupCadenceClients(requestCtx,
		*request.GetEndpoints(),
		defaultDomain,
		request.GetRetries(),
		request.GetRetryDelay(),
		&opts,
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrConnection))

		return reply
	}

	// set the timeout
	cadenceClientTimeout = request.GetClientTimeout()

	// reset the deadline on ctx with new timeout
	// and check if we need to register the default
	// domain
	if request.GetCreateDomain() {
		ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
		defer cancel()

		// register the domain

		// $debug(jack.burns): DELETE THIS!
		// THIS IS A PATCH, NEED TO COME BACK AND LOOK AT THIS
		retention := int32(365)
		err = clientHelper.RegisterDomain(ctx,
			&cadenceshared.RegisterDomainRequest{
				Name:                                   &defaultDomain,
				WorkflowExecutionRetentionPeriodInDays: &retention,
			},
		)
		if err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err))

			return reply
		}
	}

	// build reply
	buildReply(reply, nil)

	return reply
}

func handleHeartbeatRequest(requestCtx context.Context, request *messages.HeartbeatRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("HeartbeatRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new HeartbeatReply
	reply := createReplyMessage(request)
	buildReply(reply, nil)

	return reply
}

func handleInitializeRequest(requestCtx context.Context, request *messages.InitializeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new InitializeReply
	reply := createReplyMessage(request)

	// set the reply address
	if globals.DebugPrelaunched {
		replyAddress = "http://127.0.0.2:5001/"
	} else {
		address := *request.GetLibraryAddress()
		port := request.GetLibraryPort()
		replyAddress = fmt.Sprintf("http://%s:%d/",
			address,
			port,
		)
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest info", zap.String("Reply Address", replyAddress))
	buildReply(reply, nil)

	return reply
}

func handleTerminateRequest(requestCtx context.Context, request *messages.TerminateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("TerminateRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new TerminateReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleNewWorkerRequest(requestCtx context.Context, request *messages.NewWorkerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	domain := *request.GetDomain()
	taskList := *request.GetTaskList()
	logger.Debug("NewWorkerRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.String("TaskList", taskList),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new NewWorkerReply
	reply := createReplyMessage(request)

	// create a new worker using a configured ClientHelper instance
	workerID := cadenceworkers.NextWorkerID()
	worker, err := clientHelper.StartWorker(domain,
		taskList,
		*request.GetOptions(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err), workerID)

		return reply
	}

	// put the worker and workerID from the new worker to the
	workerID = Workers.Add(workerID, worker)

	// build the reply
	buildReply(reply, nil, workerID)

	return reply
}

func handleStopWorkerRequest(requestCtx context.Context, request *messages.StopWorkerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workerID := request.GetWorkerID()
	logger.Debug("StopWorkerRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("WorkerId", workerID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new StopWorkerReply
	reply := createReplyMessage(request)

	// get the workerID from the request so that we know
	// what worker to stop
	worker := Workers.Get(workerID)
	if worker == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// stop the worker and
	// remove it from the Workers map
	clientHelper.StopWorker(worker)
	workerID = Workers.Remove(workerID)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Worker has been removed from Workers", zap.Int64("WorkerID", workerID))
	buildReply(reply, nil)

	return reply
}

func handleDomainDescribeRequest(requestCtx context.Context, request *messages.DomainDescribeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	domain := *request.GetName()
	logger.Debug("DomainDescribeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new DomainDescribeReply
	reply := createReplyMessage(request)

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// send a describe domain request to the cadence server
	describeDomainResponse, err := clientHelper.DescribeDomain(ctx, domain)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build reply
	buildReply(reply, nil, describeDomainResponse)

	return reply
}

func handleDomainRegisterRequest(requestCtx context.Context, request *messages.DomainRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new DomainRegisterReply
	reply := createReplyMessage(request)

	// create a new cadence domain RegisterDomainRequest for
	// registering a new domain
	emitMetrics := request.GetEmitMetrics()
	retentionDays := request.GetRetentionDays()
	registerDomainRequest := cadenceshared.RegisterDomainRequest{
		Name:                                   request.GetName(),
		Description:                            request.GetDescription(),
		OwnerEmail:                             request.GetOwnerEmail(),
		EmitMetric:                             &emitMetrics,
		WorkflowExecutionRetentionPeriodInDays: &retentionDays,
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// register the domain using the RegisterDomainRequest
	err := clientHelper.RegisterDomain(ctx, &registerDomainRequest)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build reply
	buildReply(reply, nil)

	return reply
}

func handleDomainUpdateRequest(requestCtx context.Context, request *messages.DomainUpdateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	domain := *request.GetName()
	logger.Debug("DomainUpdateRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.Int("ProccessId", os.Getpid()),
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
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// Update the domain using the UpdateDomainRequest
	err := clientHelper.UpdateDomain(ctx, &domainUpdateRequest)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build reply
	buildReply(reply, nil)

	return reply
}

// -------------------------------------------------------------------------
// IProxyRequest workflow message type handler methods

func handleWorkflowRegisterRequest(requestCtx context.Context, request *messages.WorkflowRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowName := request.GetName()
	logger.Debug("WorkflowRegisterRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Workflow", *workflowName),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowRegisterReply
	reply := createReplyMessage(request)

	// create workflow function
	workflowFunc := func(ctx workflow.Context, input []byte) ([]byte, error) {

		// $debug(jack.burns): DELETE THIS!
		contextID := cadenceworkflows.NextContextID()
		requestID := NextRequestID()
		logger.Debug("Executing Workflow",
			zap.String("Workflow", *workflowName),
			zap.Int64("RequestId", requestID),
			zap.Int64("ContextId", contextID),
			zap.Int("ProccessId", os.Getpid()),
		)

		// set the WorkflowContext in WorkflowContexts
		wectx := cadenceworkflows.NewWorkflowContext(ctx)
		wectx.SetWorkflowName(workflowName)
		contextID = WorkflowContexts.Add(contextID, wectx)

		// Send a WorkflowInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		workflowInvokeRequest := messages.NewWorkflowInvokeRequest()
		workflowInvokeRequest.SetRequestID(requestID)
		workflowInvokeRequest.SetContextID(contextID)
		workflowInvokeRequest.SetArgs(input)

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
		op := NewOperation(requestID, workflowInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send workflowInvokeRequest
		go sendMessage(workflowInvokeRequest)

		// block and get result
		result := <-op.GetChannel()
		switch s := result.(type) {

		// workflow failed
		case error:

			// $debug(jack.burns): DELETE THIS!
			logger.Error("Workflow Failed With Error",
				zap.String("Workflow", *workflowName),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return nil, s

		// workflow succeeded
		case []byte:

			// $debug(jack.burns): DELETE THIS!
			logger.Info("Workflow Completed Successfully",
				zap.String("Workflow", *workflowName),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// register the workflow
	workflow.RegisterWithOptions(workflowFunc, workflow.RegisterOptions{Name: *workflowName})

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("workflow successfully registered", zap.String("WorkflowName", *workflowName))
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteRequest(requestCtx context.Context, request *messages.WorkflowExecuteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowName := *request.GetWorkflow()
	domain := *request.GetDomain()
	logger.Debug("WorkflowExecuteRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowName", workflowName),
		zap.String("Domain", domain),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowExecuteReply
	reply := createReplyMessage(request)

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// check for options
	var opts client.StartWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// signalwithstart the specified workflow
	workflowRun, err := clientHelper.ExecuteWorkflow(ctx,
		domain,
		opts,
		workflowName,
		request.GetArgs(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// extract the workflow ID and RunID
	workflowExecution := workflow.Execution{
		ID:    workflowRun.GetID(),
		RunID: workflowRun.GetRunID(),
	}

	// build the reply
	buildReply(reply, nil, &workflowExecution)

	return reply
}

func handleWorkflowCancelRequest(requestCtx context.Context, request *messages.WorkflowCancelRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	logger.Debug("WorkflowCancelRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowCancelReply
	reply := createReplyMessage(request)

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// cancel the specified workflow
	err := clientHelper.CancelWorkflow(ctx,
		workflowID,
		runID,
		*request.GetDomain(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowTerminateRequest(requestCtx context.Context, request *messages.WorkflowTerminateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	logger.Debug("WorkflowTerminateRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowTerminateReply
	reply := createReplyMessage(request)

	// create the context to terminate the workflow
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
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
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSignalWithStartRequest(requestCtx context.Context, request *messages.WorkflowSignalWithStartRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflow := *request.GetWorkflow()
	workflowID := *request.GetWorkflowID()
	logger.Debug("WorkflowSignalWithStartRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Workflow", workflow),
		zap.String("WorkflowId", workflowID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSignalWithStartReply
	reply := createReplyMessage(request)

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
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
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflowExecution)

	return reply
}

func handleWorkflowSetCacheSizeRequest(requestCtx context.Context, request *messages.WorkflowSetCacheSizeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSetCacheSizeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSetCacheSizeReply
	reply := createReplyMessage(request)

	// set the sticky workflow cache size
	worker.SetStickyWorkflowCacheSize(request.GetSize())

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowMutableRequest(requestCtx context.Context, request *messages.WorkflowMutableRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowMutableRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowMutableReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

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

		// check if the results are *cadencerrors.CadenceError
		if v, ok := a.(*cadenceerrors.CadenceError); ok {
			if _v, _ok := b.(*cadenceerrors.CadenceError); _ok {
				if v.GetType() == _v.GetType() &&
					v.ToString() == _v.ToString() {
					return true
				}
				return false
			}
			return false
		}

		// check if the results are []byte
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
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDescribeExecutionRequest(requestCtx context.Context, request *messages.WorkflowDescribeExecutionRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	logger.Debug("WorkflowDescribeExecutionRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowDescribeExecutionReply
	reply := createReplyMessage(request)

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// DescribeWorkflow call to cadence client
	dwer, err := clientHelper.DescribeWorkflowExecution(ctx,
		workflowID,
		runID,
		*request.GetDomain(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build reply
	buildReply(reply, nil, dwer)

	return reply
}

func handleWorkflowGetResultRequest(requestCtx context.Context, request *messages.WorkflowGetResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowGetResultRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetResultReply
	reply := createReplyMessage(request)

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
	defer cancel()

	// call GetWorkflow
	workflowRun, err := clientHelper.GetWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetDomain(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// get the result of WorkflowRun
	var result []byte
	err = workflowRun.Get(ctx, &result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowSignalSubscribeRequest(requestCtx context.Context, request *messages.WorkflowSignalSubscribeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowSignalSubscribeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", request.GetContextID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSignalSubscribeReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

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

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Received signal!", zap.String("signal", *signalName),
			zap.ByteString("args", signalArgs))

		// create the WorkflowSignalInvokeRequest
		requestID := NextRequestID()
		workflowSignalInvokeRequest := messages.NewWorkflowSignalInvokeRequest()
		workflowSignalInvokeRequest.SetRequestID(requestID)
		workflowSignalInvokeRequest.SetContextID(contextID)
		workflowSignalInvokeRequest.SetSignalArgs(signalArgs)
		workflowSignalInvokeRequest.SetSignalName(signalName)

		// set ReplayStatus
		setReplayStatus(ctx, workflowSignalInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowSignalInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(workflowSignalInvokeRequest)

		// wait for the future to be unblocked
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:

			// $debug(jack.burns): DELETE THIS!
			logger.Error("signal failed with error",
				zap.String("Signal", *signalName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Error(s),
			)

		case bool:

			// $debug(jack.burns): DELETE THIS!
			logger.Info("signal completed successfully",
				zap.String("Signal", *signalName),
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Bool("Success", s),
			)

		default:

			// $debug(jack.burns): DELETE THIS!
			logger.Info("signal result unexpected",
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
				logger.Error("Error In Workflow Context", zap.Error(err))
			}

			if done {
				return
			}
		}
	})

	return reply
}

func handleWorkflowSignalRequest(requestCtx context.Context, request *messages.WorkflowSignalRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	logger.Debug("WorkflowSignalRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSignalReply
	reply := createReplyMessage(request)

	// create the context to signal the workflow
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
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
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowHasLastResultRequest(requestCtx context.Context, request *messages.WorkflowHasLastResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowHasLastResultRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowHasLastResultReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// build the reply
	buildReply(reply, nil, workflow.HasLastCompletionResult(ctx))

	return reply
}

func handleWorkflowGetLastResultRequest(requestCtx context.Context, request *messages.WorkflowGetLastResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowGetLastResultRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetLastResultReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set replay status
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// get the last completion result from the cadence client
	var result []byte
	err := workflow.GetLastCompletionResult(ctx, &result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDisconnectContextRequest(requestCtx context.Context, request *messages.WorkflowDisconnectContextRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowDisconnectContextRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowDisconnectContextReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// create a new disconnected context
	// and then replace the existing one with the new one
	disconnectedCtx, cancel := workflow.NewDisconnectedContext(wectx.GetContext())
	wectx.SetContext(disconnectedCtx)
	wectx.SetCancelFunction(cancel)

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowGetTimeRequest(requestCtx context.Context, request *messages.WorkflowGetTimeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowGetTimeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetTimeReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set replay status
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// build the reply
	buildReply(reply, nil, workflow.Now(ctx))

	return reply
}

func handleWorkflowSleepRequest(requestCtx context.Context, request *messages.WorkflowSleepRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowSleepRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSleepReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set ReplayStatus
	ctx := wectx.GetContext()
	setReplayStatus(ctx, reply)

	// pause the current workflow for the specified duration
	err := workflow.Sleep(ctx, request.GetDuration())
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err, cadenceerrors.Cancelled))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteChildRequest(requestCtx context.Context, request *messages.WorkflowExecuteChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowExecuteChildRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowExecuteChildReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

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
	ctx, cancel := workflow.WithCancel(ctx)
	childFuture := workflow.ExecuteChildWorkflow(ctx,
		*request.GetWorkflow(),
		request.GetArgs(),
	)

	// create the new ChildContext
	cctx := cadenceworkflows.NewChildContext(ctx)
	cctx.SetCancelFunction(cancel)
	cctx.SetFuture(childFuture)

	// add the ChildWorkflowFuture and the cancel func to the
	// ChildContexts map in the parent workflow's entry
	// in the WorkflowContexts map
	childID := wectx.AddChildContext(cadenceworkflows.NextChildID(), cctx)

	// get the child workflow execution
	childWE := new(workflow.Execution)
	err := childFuture.GetChildWorkflowExecution().Get(ctx, childWE)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))
	}

	// build the reply
	buildReply(reply, nil, append(make([]interface{}, 0), childID, childWE))

	return reply
}

func handleWorkflowWaitForChildRequest(requestCtx context.Context, request *messages.WorkflowWaitForChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	childID := request.GetChildID()
	logger.Debug("WorkflowWaitForChildRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ChildId", childID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowWaitForChildReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set ReplayStatus
	ctx := cctx.GetContext()
	setReplayStatus(ctx, reply)

	// wait on the child workflow
	var result []byte
	if err := cctx.GetFuture().Get(ctx, &result); err != nil {
		var cadenceError *cadenceerrors.CadenceError
		if isCanceledErr(err) {
			cadenceError = cadenceerrors.NewCadenceError(err, cadenceerrors.Cancelled)
		} else {
			cadenceError = cadenceerrors.NewCadenceError(err)
		}

		buildReply(reply, cadenceError)

		return reply
	}

	// remove the child context
	defer func() {
		_ = wectx.RemoveChildContext(childID)
	}()

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowSignalChildRequest(requestCtx context.Context, request *messages.WorkflowSignalChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	childID := request.GetChildID()
	logger.Debug("WorkflowSignalChildRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ChildId", childID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSignalChildReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set ReplayStatus
	ctx := cctx.GetContext()
	setReplayStatus(ctx, reply)

	// signal the child workflow
	future := cctx.GetFuture().SignalChildWorkflow(ctx,
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	// wait on the future
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowCancelChildRequest(requestCtx context.Context, request *messages.WorkflowCancelChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	childID := request.GetChildID()
	logger.Debug("WorkflowCancelChildRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ChildId", childID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowCancelChildReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// set replaying
	setReplayStatus(cctx.GetContext(), reply)

	// get cancel function
	// call the cancel function
	cancel := cctx.GetCancelFunction()
	cancel()

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSetQueryHandlerRequest(requestCtx context.Context, request *messages.WorkflowSetQueryHandlerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowSetQueryHandlerRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSetQueryHandlerReply
	reply := createReplyMessage(request)

	// get the workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

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

		// set ReplayStatus
		setReplayStatus(ctx, workflowQueryInvokeRequest)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowQueryInvokeRequest)
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

			// $debug(jack.burns): DELETE THIS!
			logger.Error("Query Failed With Error",
				zap.String("Query", *queryName),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return nil, s

		// success
		case []byte:

			// $debug(jack.burns): DELETE THIS!
			logger.Info("Query Completed Successfully",
				zap.String("Query", *queryName),
				zap.Int64("ContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// Set the query handler with the
	// cadence server
	err := workflow.SetQueryHandler(ctx, *queryName, queryHandler)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowQueryRequest(requestCtx context.Context, request *messages.WorkflowQueryRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowID := *request.GetWorkflowID()
	runID := *request.GetRunID()
	logger.Debug("WorkflowQueryRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("WorkflowId", workflowID),
		zap.String("RunId", runID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowQueryReply
	reply := createReplyMessage(request)

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
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
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// extract the result
	var result []byte
	if value.HasValue() {
		err = value.Get(&result)
		if err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err))

			return reply
		}
	}

	// build reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowGetVersionRequest(requestCtx context.Context, request *messages.WorkflowGetVersionRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowGetVersionRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetVersionReply
	reply := createReplyMessage(request)

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

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

	// build the reply
	buildReply(reply, nil, version)

	return reply
}

// -------------------------------------------------------------------------
// IProxyRequest activity message type handler methods

func handleActivityRegisterRequest(requestCtx context.Context, request *messages.ActivityRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	requestID := NextRequestID()
	activityName := request.GetName()
	logger.Debug("ActivityRegisterRequest Received",
		zap.Int64("RequestId", requestID),
		zap.String("Activity", *activityName),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityRegisterReply
	reply := createReplyMessage(request)

	// define the activity function
	activityFunc := func(ctx context.Context, input []byte) ([]byte, error) {

		// $debug(jack.burns): DELETE THIS!
		contextID := cadenceactivities.NextContextID()
		logger.Debug("Executing Activity",
			zap.String("Activity", *activityName),
			zap.Int64("ActivityContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProccessId", os.Getpid()),
		)

		// add the context to ActivityContexts
		actx := cadenceactivities.NewActivityContext(ctx)
		actx.SetActivityName(activityName)
		contextID = ActivityContexts.Add(contextID, actx)

		// Send a ActivityInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		activityInvokeRequest := messages.NewActivityInvokeRequest()
		activityInvokeRequest.SetRequestID(requestID)
		activityInvokeRequest.SetArgs(input)
		activityInvokeRequest.SetContextID(contextID)
		activityInvokeRequest.SetActivity(request.GetName())

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeRequest)
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
			activityStoppingRequest.SetActivityID(request.GetName())
			activityStoppingRequest.SetContextID(contextID)

			// create the Operation for this request and add it to the operations map
			stoppingReplyChan := make(chan interface{})
			op := NewOperation(requestID, activityStoppingRequest)
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

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {

		// failure
		case error:

			// $debug(jack.burns): DELETE THIS!
			logger.Error("Activity Failed With Error",
				zap.String("Activity", *activityName),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return nil, s

		// success
		case []byte:

			// $debug(jack.burns): DELETE THIS!
			logger.Info("Activity Completed Successfully",
				zap.String("Activity", *activityName),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// register the activity
	activity.RegisterWithOptions(activityFunc, activity.RegisterOptions{Name: *activityName})

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Activity Successfully Registered", zap.String("ActivityName", *activityName))
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteRequest(requestCtx context.Context, request *messages.ActivityExecuteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("ActivityExecuteRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.String("ActivityName", *request.GetActivity()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityExecuteReply
	reply := createReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// get the activity options, the context,
	// and set the activity options on the context
	opts := request.GetOptions()
	ctx := workflow.WithActivityOptions(wectx.GetContext(), *opts)

	// execute the activity
	var result []byte
	if err := workflow.ExecuteActivity(ctx, *request.GetActivity(), request.GetArgs()).Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleActivityHasHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityHasHeartbeatDetailsRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityHasHeartbeatDetailsRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityHasHeartbeatDetailsReply
	reply := createReplyMessage(request)

	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// build the reply
	buildReply(reply, nil, activity.HasHeartbeatDetails(actx.GetContext()))

	return reply
}

func handleActivityGetHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityGetHeartbeatDetailsRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityGetHeartbeatDetailsRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityGetHeartbeatDetailsReply
	reply := createReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// get the activity heartbeat details
	var details []byte
	err := activity.GetHeartbeatDetails(actx.GetContext(), &details)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil, details)

	return reply
}

func handleActivityRecordHeartbeatRequest(requestCtx context.Context, request *messages.ActivityRecordHeartbeatRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityRecordHeartbeatRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityRecordHeartbeatReply
	reply := createReplyMessage(request)

	// check to see if external or internal
	// record heartbeat
	var err error
	details := request.GetDetails()
	if request.GetTaskToken() == nil {
		if request.GetActivityID() == nil {
			actx := ActivityContexts.Get(request.GetContextID())
			if actx == nil {
				buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

				return reply
			}
			activity.RecordHeartbeat(ActivityContexts.Get(request.GetContextID()).GetContext(), details)
		} else {
			ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
			defer cancel()

			// RecordActivityHeartbeatByID
			err = clientHelper.RecordActivityHeartbeatByID(ctx,
				*request.GetDomain(),
				*request.GetWorkflowID(),
				*request.GetRunID(),
				*request.GetActivityID(),
				details,
			)
		}

	} else {

		// create the new context
		ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
		defer cancel()

		// record the heartbeat details
		err = clientHelper.RecordActivityHeartbeat(ctx,
			request.GetTaskToken(),
			*request.GetDomain(),
			details,
		)
	}
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleActivityGetInfoRequest(requestCtx context.Context, request *messages.ActivityGetInfoRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("ActivityGetInfoRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityGetInfoReply
	reply := createReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(contextID)
	if actx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrConnection))

		return reply
	}

	// get info
	// build the reply
	info := activity.GetInfo(actx.GetContext())
	buildReply(reply, nil, &info)

	return reply
}

func handleActivityCompleteRequest(requestCtx context.Context, request *messages.ActivityCompleteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityCompleteRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityCompleteReply
	reply := createReplyMessage(request)

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, cadenceClientTimeout)
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
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteLocalRequest(requestCtx context.Context, request *messages.ActivityExecuteLocalRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	activityTypeID := request.GetActivityTypeID()
	logger.Debug("ActivityExecuteLocalRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("ActivityTypeId", activityTypeID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityExecuteLocalReply
	reply := createReplyMessage(request)

	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(globals.ErrEntityNotExist))

		return reply
	}

	// the local activity function
	args := request.GetArgs()
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {

		// create an activity context entry in the ActivityContexts map
		// add the context to ActivityContexts
		actx := cadenceactivities.NewActivityContext(ctx)
		activityContextID := ActivityContexts.Add(cadenceactivities.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Cadence Lib
		// cadence-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeLocalRequest)
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

			// $debug(jack.burns): DELETE THIS!
			logger.Error("Activity Failed With Error",
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Error(s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return nil, s

		// success
		case []byte:

			// $debug(jack.burns): DELETE THIS!
			logger.Info("Activity Successful",
				zap.Int64("RequestId", requestID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Any("Result", s),
				zap.Int("ProccessId", os.Getpid()),
			)

			return s, nil

		// unexpected result
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// get the activity options
	var opts workflow.LocalActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// and set the activity options on the context
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)

	// wait for the future to be unblocked
	var result []byte
	if err := workflow.ExecuteLocalActivity(ctx, localActivityFunc, args).Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err))

		return reply
	}

	// build reply
	buildReply(reply, nil, result)

	return reply
}
