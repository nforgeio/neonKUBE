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
	"errors"
	"fmt"
	"net/http"
	"os"
	"reflect"
	"time"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/cadence-proxy/internal/cadence/cadenceactivities"
	cadenceclient "github.com/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

var (

	// ClientHelper is a global variable that holds this cadence-proxy's instance
	// of the CadenceClientHelper that will be used to create domain and workflow clients
	// that communicate with the cadence server
	clientHelper = cadenceclient.NewCadenceClientHelper()
)

// -------------------------------------------------------------------------
// IProxyRequest message type handler entrypoint

func handleIProxyRequest(request messages.IProxyRequest) error {

	// look for IsCancelled
	if request.GetIsCancellable() {
		ctx, cancel := context.WithCancel(context.Background())
		c := NewCancellable(ctx, cancel)
		_ = Cancellables.Add(request.GetRequestID(), c)
	}

	// handle the messages individually
	// based on their message type
	var err error
	var reply messages.IProxyReply
	switch request.GetType() {

	// -------------------------------------------------------------------------
	// Client message types

	// InitializeRequest
	case messagetypes.InitializeRequest:
		if v, ok := request.(*messages.InitializeRequest); ok {
			reply = handleInitializeRequest(v)
		}

	// HeartbeatRequest
	case messagetypes.HeartbeatRequest:
		if v, ok := request.(*messages.HeartbeatRequest); ok {
			reply = handleHeartbeatRequest(v)
		}

	// CancelRequest
	case messagetypes.CancelRequest:
		if v, ok := request.(*messages.CancelRequest); ok {
			reply = handleCancelRequest(v)
		}

	// ConnectRequest
	case messagetypes.ConnectRequest:
		if v, ok := request.(*messages.ConnectRequest); ok {
			reply = handleConnectRequest(v)
		}

	// DomainDescribeRequest
	case messagetypes.DomainDescribeRequest:
		if v, ok := request.(*messages.DomainDescribeRequest); ok {
			reply = handleDomainDescribeRequest(v)
		}

	// DomainRegisterRequest
	case messagetypes.DomainRegisterRequest:
		if v, ok := request.(*messages.DomainRegisterRequest); ok {
			reply = handleDomainRegisterRequest(v)
		}

	// DomainUpdateRequest
	case messagetypes.DomainUpdateRequest:
		if v, ok := request.(*messages.DomainUpdateRequest); ok {
			reply = handleDomainUpdateRequest(v)
		}

	// TerminateRequest
	case messagetypes.TerminateRequest:
		if v, ok := request.(*messages.TerminateRequest); ok {
			reply = handleTerminateRequest(v)
		}

	// NewWorkerRequest
	case messagetypes.NewWorkerRequest:
		if v, ok := request.(*messages.NewWorkerRequest); ok {
			reply = handleNewWorkerRequest(v)
		}

	// StopWorkerRequest
	case messagetypes.StopWorkerRequest:
		if v, ok := request.(*messages.StopWorkerRequest); ok {
			reply = handleStopWorkerRequest(v)
		}

	// PingRequest
	case messagetypes.PingRequest:
		if v, ok := request.(*messages.PingRequest); ok {
			reply = handlePingRequest(v)
		}

	// -------------------------------------------------------------------------
	// Workflow message types

	// WorkflowRegisterRequest
	case messagetypes.WorkflowRegisterRequest:
		if v, ok := request.(*messages.WorkflowRegisterRequest); ok {
			reply = handleWorkflowRegisterRequest(v)
		}

	// WorkflowExecuteRequest
	case messagetypes.WorkflowExecuteRequest:
		if v, ok := request.(*messages.WorkflowExecuteRequest); ok {
			reply = handleWorkflowExecuteRequest(v)
		}

	// WorkflowCancelRequest
	case messagetypes.WorkflowCancelRequest:
		if v, ok := request.(*messages.WorkflowCancelRequest); ok {
			reply = handleWorkflowCancelRequest(v)
		}

	// WorkflowTerminateRequest
	case messagetypes.WorkflowTerminateRequest:
		if v, ok := request.(*messages.WorkflowTerminateRequest); ok {
			reply = handleWorkflowTerminateRequest(v)
		}

	// WorkflowSignalWithStartRequest
	case messagetypes.WorkflowSignalWithStartRequest:
		if v, ok := request.(*messages.WorkflowSignalWithStartRequest); ok {
			reply = handleWorkflowSignalWithStartRequest(v)
		}

	// WorkflowSetCacheSizeRequest
	case messagetypes.WorkflowSetCacheSizeRequest:
		if v, ok := request.(*messages.WorkflowSetCacheSizeRequest); ok {
			reply = handleWorkflowSetCacheSizeRequest(v)
		}

	// WorkflowQueryRequest
	case messagetypes.WorkflowQueryRequest:
		if v, ok := request.(*messages.WorkflowQueryRequest); ok {
			reply = handleWorkflowQueryRequest(v)
		}

	// WorkflowMutableRequest
	case messagetypes.WorkflowMutableRequest:
		if v, ok := request.(*messages.WorkflowMutableRequest); ok {
			reply = handleWorkflowMutableRequest(v)
		}

	// WorkflowDescribeExecutionRequest
	case messagetypes.WorkflowDescribeExecutionRequest:
		if v, ok := request.(*messages.WorkflowDescribeExecutionRequest); ok {
			reply = handleWorkflowDescribeExecutionRequest(v)
		}

	// WorkflowGetResultRequest
	case messagetypes.WorkflowGetResultRequest:
		if v, ok := request.(*messages.WorkflowGetResultRequest); ok {
			reply = handleWorkflowGetResultRequest(v)
		}

	// WorkflowSignalSubscribeRequest
	case messagetypes.WorkflowSignalSubscribeRequest:
		if v, ok := request.(*messages.WorkflowSignalSubscribeRequest); ok {
			reply = handleWorkflowSignalSubscribeRequest(v)
		}

	// WorkflowSignalRequest
	case messagetypes.WorkflowSignalRequest:
		if v, ok := request.(*messages.WorkflowSignalRequest); ok {
			reply = handleWorkflowSignalRequest(v)
		}

	// WorkflowHasLastResultRequest
	case messagetypes.WorkflowHasLastResultRequest:
		if v, ok := request.(*messages.WorkflowHasLastResultRequest); ok {
			reply = handleWorkflowHasLastResultRequest(v)
		}

	// WorkflowGetLastResultRequest
	case messagetypes.WorkflowGetLastResultRequest:
		if v, ok := request.(*messages.WorkflowGetLastResultRequest); ok {
			reply = handleWorkflowGetLastResultRequest(v)
		}

	// WorkflowDisconnectContextRequest
	case messagetypes.WorkflowDisconnectContextRequest:
		if v, ok := request.(*messages.WorkflowDisconnectContextRequest); ok {
			reply = handleWorkflowDisconnectContextRequest(v)
		}

	// WorkflowGetTimeRequest
	case messagetypes.WorkflowGetTimeRequest:
		if v, ok := request.(*messages.WorkflowGetTimeRequest); ok {
			reply = handleWorkflowGetTimeRequest(v)
		}

	// WorkflowSleepRequest
	case messagetypes.WorkflowSleepRequest:
		if v, ok := request.(*messages.WorkflowSleepRequest); ok {
			reply = handleWorkflowSleepRequest(v)
		}

	// WorkflowExecuteChildRequest
	case messagetypes.WorkflowExecuteChildRequest:
		if v, ok := request.(*messages.WorkflowExecuteChildRequest); ok {
			reply = handleWorkflowExecuteChildRequest(v)
		}

	// WorkflowWaitForChildRequest
	case messagetypes.WorkflowWaitForChildRequest:
		if v, ok := request.(*messages.WorkflowWaitForChildRequest); ok {
			reply = handleWorkflowWaitForChildRequest(v)
		}

	// WorkflowSignalChildRequest
	case messagetypes.WorkflowSignalChildRequest:
		if v, ok := request.(*messages.WorkflowSignalChildRequest); ok {
			reply = handleWorkflowSignalChildRequest(v)
		}

	// WorkflowCancelChildRequest
	case messagetypes.WorkflowCancelChildRequest:
		if v, ok := request.(*messages.WorkflowCancelChildRequest); ok {
			reply = handleWorkflowCancelChildRequest(v)
		}

	// WorkflowSetQueryHandlerRequest
	case messagetypes.WorkflowSetQueryHandlerRequest:
		if v, ok := request.(*messages.WorkflowSetQueryHandlerRequest); ok {
			reply = handleWorkflowSetQueryHandlerRequest(v)
		}

	// -------------------------------------------------------------------------
	// Activity message types

	// ActivityExecuteRequest
	case messagetypes.ActivityExecuteRequest:
		if v, ok := request.(*messages.ActivityExecuteRequest); ok {
			reply = handleActivityExecuteRequest(v)
		}

	// ActivityRegisterRequest
	case messagetypes.ActivityRegisterRequest:
		if v, ok := request.(*messages.ActivityRegisterRequest); ok {
			reply = handleActivityRegisterRequest(v)
		}

	// ActivityHasHeartbeatDetailsRequest
	case messagetypes.ActivityHasHeartbeatDetailsRequest:
		if v, ok := request.(*messages.ActivityHasHeartbeatDetailsRequest); ok {
			reply = handleActivityHasHeartbeatDetailsRequest(v)
		}

	// ActivityGetHeartbeatDetailsRequest
	case messagetypes.ActivityGetHeartbeatDetailsRequest:
		if v, ok := request.(*messages.ActivityGetHeartbeatDetailsRequest); ok {
			reply = handleActivityGetHeartbeatDetailsRequest(v)
		}

	// ActivityRecordHeartbeatRequest
	case messagetypes.ActivityRecordHeartbeatRequest:
		if v, ok := request.(*messages.ActivityRecordHeartbeatRequest); ok {
			reply = handleActivityRecordHeartbeatRequest(v)
		}

	// ActivityGetInfoRequest
	case messagetypes.ActivityGetInfoRequest:
		if v, ok := request.(*messages.ActivityGetInfoRequest); ok {
			reply = handleActivityGetInfoRequest(v)
		}

	// ActivityCompleteRequest
	case messagetypes.ActivityCompleteRequest:
		if v, ok := request.(*messages.ActivityCompleteRequest); ok {
			reply = handleActivityCompleteRequest(v)
		}

	// ActivityExecuteLocalRequest
	case messagetypes.ActivityExecuteLocalRequest:
		if v, ok := request.(*messages.ActivityExecuteLocalRequest); ok {
			reply = handleActivityExecuteLocalRequest(v)
		}

	// Undefined message type
	default:

		// $debug(jack.burns): DELETE THIS!
		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", request.GetType())
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any errors that may have occurred
	// in the switch block or if the message could not
	// be cast to a specific type
	if err != nil {
		return err
	}

	// send the reply as an http.Request back to the
	// Neon.Cadence Library via http.PUT
	resp, err := putToNeonCadenceClient(reply)
	if err != nil {
		return err
	}
	defer func() {

		// $debug(jack.burns): DELETE THIS!
		err := resp.Body.Close()
		if err != nil {
			logger.Error("could not close response body", zap.Error(err))
		}
	}()

	return nil
}

// -------------------------------------------------------------------------
// IProxyRequest client message type handler methods

func handleCancelRequest(request *messages.CancelRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("CancelRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)
	buildReply(reply, nil, true)

	return reply
}

func handleConnectRequest(request *messages.ConnectRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ConnectRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ConnectReply
	reply := createReplyMessage(request)

	// client options
	opts := client.Options{
		Identity: *request.GetIdentity(),
	}

	// configure the CadenceClientHelper
	clientHelper = cadenceclient.NewCadenceClientHelper()
	clientHelper.SetHostPort(*request.GetEndpoints())
	clientHelper.SetClientOptions(&opts)
	err := clientHelper.SetupServiceConfig()
	if err != nil {
		clientHelper = nil
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// make a channel that waits for a connection to be established
	// until returning ready
	connectChan := make(chan error)
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)

	// defer the cancel of the context and
	// closing of the connectChan
	defer func() {
		cancel()
		close(connectChan)
	}()

	go func() {

		// defer sending error
		// or nil over connectChan
		var err error
		defer func() {
			connectChan <- err
		}()

		// make a domain describe request on the cadence system domain
		// to check if it is ready to accept requests
		_, err = clientHelper.DescribeDomain(ctx, _cadenceSystemDomain)
	}()

	// block and catch the result
	if connectResult := <-connectChan; connectResult != nil {
		clientHelper = nil
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// set the timeout
	cadenceClientTimeout = request.GetClientTimeout()
	buildReply(reply, nil)

	return reply
}

func handleDomainDescribeRequest(request *messages.DomainDescribeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	domain := *request.GetName()
	logger.Debug("DomainDescribeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new DomainDescribeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// send a describe domain request to the cadence server
	describeDomainResponse, err := clientHelper.DescribeDomain(ctx, domain)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build reply
	buildReply(reply, nil, describeDomainResponse)

	return reply
}

func handleDomainRegisterRequest(request *messages.DomainRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new DomainRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

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
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// register the domain using the RegisterDomainRequest
	err := clientHelper.RegisterDomain(ctx, &registerDomainRequest)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build reply
	buildReply(reply, nil)

	return reply
}

func handleDomainUpdateRequest(request *messages.DomainUpdateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	domain := *request.GetName()
	logger.Debug("DomainUpdateRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Domain", domain),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new DomainUpdateReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// DomainUpdateRequest.Configuration
	configuration := new(cadenceshared.DomainConfiguration)
	configurationEmitMetrics := request.GetConfigurationEmitMetrics()
	configurationRetentionDays := request.GetConfigurationRetentionDays()
	configuration.EmitMetric = &configurationEmitMetrics
	configuration.WorkflowExecutionRetentionPeriodInDays = &configurationRetentionDays

	// DomainUpdateRequest.UpdatedInfo
	updatedInfo := new(cadenceshared.UpdateDomainInfo)
	updatedInfo.Description = request.GetUpdatedInfoDescription()
	updatedInfo.OwnerEmail = request.GetUpdatedInfoOwnerEmail()

	// DomainUpdateRequest
	domainUpdateRequest := cadenceshared.UpdateDomainRequest{
		Name:          &domain,
		Configuration: configuration,
		UpdatedInfo:   updatedInfo,
	}

	// create context with timeout
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// Update the domain using the UpdateDomainRequest
	err := clientHelper.UpdateDomain(ctx, &domainUpdateRequest)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build reply
	buildReply(reply, nil)

	return reply
}

func handleHeartbeatRequest(request *messages.HeartbeatRequest) messages.IProxyReply {

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

func handleInitializeRequest(request *messages.InitializeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new InitializeReply
	reply := createReplyMessage(request)

	// set the reply address
	if DebugPrelaunched {
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

func handleTerminateRequest(request *messages.TerminateRequest) messages.IProxyReply {

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

func handleNewWorkerRequest(request *messages.NewWorkerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("NewWorkerRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new NewWorkerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create a new worker using a configured CadenceClientHelper instance
	workerID := cadenceworkers.NextWorkerID()
	domain := *request.GetDomain()
	taskList := *request.GetTaskList()
	worker, err := clientHelper.StartWorker(domain,
		taskList,
		*request.GetOptions(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()), workerID)

		return reply
	}

	// put the worker and workerID from the new worker to the
	workerID = Workers.Add(workerID, worker)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Worker has been added to Workers", zap.Int64("WorkerID", workerID))

	// build the reply
	buildReply(reply, nil, workerID)

	return reply
}

func handleStopWorkerRequest(request *messages.StopWorkerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workerID := request.GetWorkerID()
	logger.Debug("StopWorkerRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("WorkerId", workerID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new StopWorkerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the workerID from the request so that we know
	// what worker to stop
	worker := Workers.Get(workerID)
	if worker == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

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

func handlePingRequest(request *messages.PingRequest) messages.IProxyReply {

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

// -------------------------------------------------------------------------
// IProxyRequest workflow message type handler methods

func handleWorkflowRegisterRequest(request *messages.WorkflowRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	workflowName := request.GetName()
	logger.Debug("WorkflowRegisterRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.String("Workflow", *workflowName),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

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

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send the WorkflowInvokeRequest
		resp, err := putToNeonCadenceClient(workflowInvokeRequest)
		if err != nil {
			panic(err)
		}
		defer func() {

			// $debug(jack.burns): DELETE THIS!
			err := resp.Body.Close()
			if err != nil {
				logger.Error("could not close response body", zap.Error(err))
			}
		}()

		// block and get result
		result := <-op.GetChannel()
		switch s := result.(type) {

		// workflow failed
		case error:

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Workflow Failed With Error",
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
			logger.Debug("Workflow Completed Successfully",
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

func handleWorkflowExecuteRequest(request *messages.WorkflowExecuteRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// check for options
	var opts client.StartWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
		if opts.DecisionTaskStartToCloseTimeout <= 0 {
			opts.DecisionTaskStartToCloseTimeout = cadenceClientTimeout
		}

	} else {
		opts = client.StartWorkflowOptions{
			ExecutionStartToCloseTimeout:    cadenceClientTimeout,
			DecisionTaskStartToCloseTimeout: cadenceClientTimeout,
		}
	}

	// signalwithstart the specified workflow
	workflowRun, err := clientHelper.ExecuteWorkflow(ctx,
		domain,
		opts,
		workflowName,
		request.GetArgs(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

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

func handleWorkflowCancelRequest(request *messages.WorkflowCancelRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// cancel the specified workflow
	err := clientHelper.CancelWorkflow(ctx,
		workflowID,
		runID,
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowTerminateRequest(request *messages.WorkflowTerminateRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context to terminate the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// terminate the specified workflow
	err := clientHelper.TerminateWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetReason(),
		request.GetDetails(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSignalWithStartRequest(request *messages.WorkflowSignalWithStartRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signalwithstart the specified workflow
	workflowExecution, err := clientHelper.SignalWithStartWorkflow(ctx,
		workflowID,
		*request.GetSignalName(),
		request.GetSignalArgs(),
		*request.GetOptions(),
		workflow,
		request.GetWorkflowArgs(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflowExecution)

	return reply
}

func handleWorkflowSetCacheSizeRequest(request *messages.WorkflowSetCacheSizeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSetCacheSizeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSetCacheSizeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// set the sticky workflow cache size
	worker.SetStickyWorkflowCacheSize(request.GetSize())

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowMutableRequest(request *messages.WorkflowMutableRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowMutableRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowMutableReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// f function for workflow.MutableSideEffect
	mutableFunc := func(ctx workflow.Context) interface{} {
		return request.GetResult()
	}

	// the equals function for workflow.MutableSideEffect
	equals := func(a, b interface{}) bool {

		// do not update if update is false
		if !request.GetUpdate() {
			return true
		}

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

	// TODO: JACK -- CADENCE CLIENT BUG
	// https://stackoverflow.com/questions/56658582/mutablesideeffect-panics-when-setting-second-value
	// https://github.com/uber-go/cadence-client/issues/763
	//
	// This is the workaround.  Here's our tracking bug:
	// https://github.com/nforgeio/neonKUBE/issues/562
	//
	// FYI: This is deprecated and will never be called now.
	ctx := wectx.GetContext()
	err := workflow.Sleep(ctx, time.Second)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// execute the cadence server MutableSideEffect call
	sideEffectValue := workflow.MutableSideEffect(ctx,
		*request.GetMutableID(),
		mutableFunc,
		equals,
	)

	// extract the result
	var result []byte
	err = sideEffectValue.Get(&result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDescribeExecutionRequest(request *messages.WorkflowDescribeExecutionRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// DescribeWorkflow call to cadence client
	describeWorkflowExecutionResponse, err := clientHelper.DescribeWorkflowExecution(ctx,
		workflowID,
		runID,
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build reply
	buildReply(reply, nil, describeWorkflowExecutionResponse)

	return reply
}

func handleWorkflowGetResultRequest(request *messages.WorkflowGetResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowGetResultRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetResultReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// call GetWorkflow
	workflowRun, err := clientHelper.GetWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// get the result of WorkflowRun
	var result []byte
	err = workflowRun.Get(ctx, &result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowSignalSubscribeRequest(request *messages.WorkflowSignalSubscribeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowSignalSubscribeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", request.GetContextID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSignalSubscribeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

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
			zap.Binary("args", signalArgs))

		// create the WorkflowSignalInvokeRequest
		workflowSignalInvokeRequest := messages.NewWorkflowSignalInvokeRequest()
		workflowSignalInvokeRequest.SetSignalArgs(signalArgs)
		workflowSignalInvokeRequest.SetSignalName(signalName)
		workflowSignalInvokeRequest.SetContextID(contextID)

		// create the Operation for this request and add it to the operations map
		requestID := NextRequestID()
		future, settable := workflow.NewFuture(ctx)
		op := NewOperation(requestID, workflowSignalInvokeRequest)
		op.SetFuture(future)
		op.SetSettable(settable)
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send a request to the
		// Neon.Cadence Lib
		f := func(ctx workflow.Context) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(workflowSignalInvokeRequest)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// send the request
		workflow.Go(ctx, f)

		// wait for the future to be unblocked
		var result interface{}
		if err := future.Get(ctx, &result); err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))
		} else {
			buildReply(reply, nil)
		}
	})

	// wait on the channel
	selector.Select(ctx)

	return reply
}

func handleWorkflowSignalRequest(request *messages.WorkflowSignalRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context to signal the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signal the specified workflow
	err := clientHelper.SignalWorkflow(ctx,
		workflowID,
		runID,
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowHasLastResultRequest(request *messages.WorkflowHasLastResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowHasLastResultRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowHasLastResultReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflow.HasLastCompletionResult(wectx.GetContext()))

	return reply
}

func handleWorkflowGetLastResultRequest(request *messages.WorkflowGetLastResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowGetLastResultRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetLastResultReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// get the last completion result from the cadence client
	var result []byte
	err := workflow.GetLastCompletionResult(wectx.GetContext(), &result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDisconnectContextRequest(request *messages.WorkflowDisconnectContextRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowDisconnectContextRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowDisconnectContextReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

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

func handleWorkflowGetTimeRequest(request *messages.WorkflowGetTimeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowGetTimeRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowGetTimeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflow.Now(wectx.GetContext()))

	return reply
}

func handleWorkflowSleepRequest(request *messages.WorkflowSleepRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowSleepRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSleepReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// pause the current workflow for the specified duration
	err := workflow.Sleep(wectx.GetContext(), request.GetDuration())
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteChildRequest(request *messages.WorkflowExecuteChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowExecuteChildRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowExecuteChildReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// set options on the context
	var opts workflow.ChildWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	} else {
		opts = workflow.ChildWorkflowOptions{
			ExecutionStartToCloseTimeout: cadenceClientTimeout,
			TaskStartToCloseTimeout:      cadenceClientTimeout,
		}
	}

	// set cancellation on the context
	// execute the child workflow
	ctx := workflow.WithChildOptions(wectx.GetContext(), opts)
	ctx, cancel := workflow.WithCancel(ctx)
	childFuture := workflow.ExecuteChildWorkflow(ctx,
		*request.GetWorkflow(),
		request.GetArgs(),
	)

	// create the new ChildContext
	cctx := cadenceworkflows.NewChildContext()
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
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))
	}

	// build the reply
	buildReply(reply, nil, append(make([]interface{}, 0), childID, childWE))

	return reply
}

func handleWorkflowWaitForChildRequest(request *messages.WorkflowWaitForChildRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// wait on the child workflow
	var result []byte
	if err := cctx.GetFuture().Get(wectx.GetContext(), &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

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

func handleWorkflowSignalChildRequest(request *messages.WorkflowSignalChildRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// signal the child workflow
	ctx := wectx.GetContext()
	future := cctx.GetFuture().SignalChildWorkflow(ctx,
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	// wait on the future
	var result []byte
	if err := future.Get(ctx, result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowCancelChildRequest(request *messages.WorkflowCancelChildRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the child context from the parent workflow context
	wectx := WorkflowContexts.Get(contextID)
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// get cancel function
	// call the cancel function
	cancel := cctx.GetCancelFunction()
	go cancel()

	// remove the child context
	defer func() {
		_ = wectx.RemoveChildContext(childID)
	}()

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSetQueryHandlerRequest(request *messages.WorkflowSetQueryHandlerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("WorkflowSetQueryHandlerRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new WorkflowSetQueryHandlerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the workflow context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// define the handler function
	ctx := wectx.GetContext()
	queryName := request.GetQueryName()
	queryHandler := func(queryArgs []byte) ([]byte, error) {

		// create the WorkflowSignalInvokeRequest
		workflowQueryInvokeRequest := messages.NewWorkflowQueryInvokeRequest()
		workflowQueryInvokeRequest.SetQueryArgs(queryArgs)
		workflowQueryInvokeRequest.SetQueryName(queryName)
		workflowQueryInvokeRequest.SetContextID(contextID)

		// create the Operation for this request and add it to the operations map
		requestID := NextRequestID()
		future, settable := workflow.NewFuture(ctx)
		op := NewOperation(requestID, workflowQueryInvokeRequest)
		op.SetFuture(future)
		op.SetSettable(settable)
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send a request to the
		// Neon.Cadence Lib
		f := func(ctx workflow.Context) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(workflowQueryInvokeRequest)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// send the request
		workflow.Go(ctx, f)

		// wait for the future to be unblocked
		var result []byte
		if err := future.Get(ctx, &result); err != nil {
			return nil, err
		}

		return result, nil
	}

	// Set the query handler with the
	// cadence server
	err := workflow.SetQueryHandler(ctx, *queryName, queryHandler)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowQueryRequest(request *messages.WorkflowQueryRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// query the workflow via the cadence client
	value, err := clientHelper.QueryWorkflow(ctx,
		workflowID,
		runID,
		*request.GetQueryName(),
		request.GetQueryArgs(),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// extract the result
	var result []byte
	if value.HasValue() {
		err = value.Get(&result)
		if err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

			return reply
		}
	}

	// build reply
	buildReply(reply, nil, result)

	return reply
}

// -------------------------------------------------------------------------
// IProxyRequest activity message type handler methods

func handleActivityRegisterRequest(request *messages.ActivityRegisterRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

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
		invokeReplyChannel := make(chan interface{})
		op := NewOperation(requestID, activityInvokeRequest)
		op.SetChannel(invokeReplyChannel)
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// get worker stop channel on the context
		stopChan := activity.GetWorkerStopChannel(ctx)

		// send a request to the
		// Neon.Cadence Lib
		f := func(message messages.IProxyRequest) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(message)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

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
			go f(activityStoppingRequest)
			<-stoppingReplyChan
		}

		// run go routines
		go s()
		go f(activityInvokeRequest)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {

		// failure
		case error:

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Activity Failed With Error",
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
			logger.Debug("Activity Completed Successfully",
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

func handleActivityExecuteRequest(request *messages.ActivityExecuteRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// get the activity options, the context,
	// and set the activity options on the context
	opts := request.GetOptions()
	ctx := workflow.WithActivityOptions(wectx.GetContext(), *opts)

	// execute the activity
	var result []byte
	if err := workflow.ExecuteActivity(ctx, *request.GetActivity(), request.GetArgs()).Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleActivityHasHeartbeatDetailsRequest(request *messages.ActivityHasHeartbeatDetailsRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityHasHeartbeatDetailsRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityHasHeartbeatDetailsReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the new context and a []byte to
	// drop the heartbeat details into
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// build the reply
	buildReply(reply, nil, activity.HasHeartbeatDetails(ctx))

	return reply
}

func handleActivityGetHeartbeatDetailsRequest(request *messages.ActivityGetHeartbeatDetailsRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityGetHeartbeatDetailsRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityGetHeartbeatDetailsReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the new context and a []byte to
	// drop the heartbeat details into
	var details []byte
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// get the activity heartbeat details
	err := activity.GetHeartbeatDetails(ctx, &details)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, details)

	return reply
}

func handleActivityRecordHeartbeatRequest(request *messages.ActivityRecordHeartbeatRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityRecordHeartbeatRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityRecordHeartbeatReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the new context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// record the heartbeat details
	activity.RecordHeartbeat(ctx, request.GetDetails())

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleActivityGetInfoRequest(request *messages.ActivityGetInfoRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	contextID := request.GetContextID()
	logger.Debug("ActivityGetInfoRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityGetInfoReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the activity context
	actx := ActivityContexts.Get(contextID)
	if actx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get info
	// build the reply
	info := activity.GetInfo(actx.GetContext())
	buildReply(reply, nil, &info)

	return reply
}

func handleActivityCompleteRequest(request *messages.ActivityCompleteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityCompleteRequest Received",
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProccessId", os.Getpid()),
	)

	// new ActivityCompleteReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// complete the activity
	err := clientHelper.CompleteActivity(ctx,
		request.GetTaskToken(),
		request.GetResult(),
		errors.New(request.GetError().ToString()),
	)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteLocalRequest(request *messages.ActivityExecuteLocalRequest) messages.IProxyReply {

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

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

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

		// send a request to the
		// Neon.Cadence Lib
		f := func(message messages.IProxyRequest) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(message)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// send the request
		go f(activityInvokeLocalRequest)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {

		// failure
		case error:

			// $debug(jack.burns): DELETE THIS!
			logger.Debug("Activity Failed With Error",
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
			logger.Debug("Activity Successful",
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
	if v := request.GetOptions(); v == nil {
		opts = *v
		if opts.ScheduleToCloseTimeout <= 0 {
			opts.ScheduleToCloseTimeout = cadenceClientTimeout
		}
	} else {
		opts = workflow.LocalActivityOptions{
			ScheduleToCloseTimeout: cadenceClientTimeout,
		}
	}

	// and set the activity options on the context
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)

	// wait for the future to be unblocked
	var result []byte
	if err := workflow.ExecuteLocalActivity(ctx, localActivityFunc, args).Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))
	} else {
		buildReply(reply, nil, result)
	}

	return reply
}

// -------------------------------------------------------------------------
// Helpers for sending ProxyReply messages back to Neon.Cadence Library

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {

	// serialize the message
	proxyMessage := message.GetProxyMessage()
	content, err := proxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, replyAddress, buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Error creating Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	// set the request header to specified content type
	// and disable http request compression
	req.Header.Set("Content-Type", ContentType)
	req.Header.Set("Accept-Encoding", "identity")

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Error sending Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	return resp, nil
}
