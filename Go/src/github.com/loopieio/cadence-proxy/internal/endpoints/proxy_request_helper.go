package endpoints

import (
	"bytes"
	"context"
	"fmt"
	"net/http"
	"os"
	"reflect"
	"time"

	cadenceclient "github.com/loopieio/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"
)

var (

	// ClientHelper is a global variable that holds this cadence-proxy's instance
	// of the CadenceClientHelper that will be used to create domain and workflow clients
	// that communicate with the cadence server
	clientHelper = cadenceclient.NewCadenceClientHelper()
)

// -------------------------------------------------------------------------
// IProxyRequest message type handler entrypoint

func handleIProxyRequest(request messages.IProxyRequest, typeCode messagetypes.MessageType) error {

	// error for catching exceptions in the switch block
	var err error
	var reply messages.IProxyMessage

	// handle the messages individually based on their message type
	switch typeCode {

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

	// WorkflowInvokeRequest
	case messagetypes.WorkflowInvokeRequest:
		if v, ok := request.(*messages.WorkflowInvokeRequest); ok {
			reply = handleWorkflowInvokeRequest(v)
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

	// WorkflowSignalRequest
	case messagetypes.WorkflowSignalRequest:
		if v, ok := request.(*messages.WorkflowSignalRequest); ok {
			reply = handleWorkflowSignalRequest(v)
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

	// WorkflowMutableInvokeRequest
	case messagetypes.WorkflowMutableInvokeRequest:
		if v, ok := request.(*messages.WorkflowMutableInvokeRequest); ok {
			reply = handleWorkflowMutableInvokeRequest(v)
		}

	// PingRequest
	case messagetypes.PingRequest:
		if v, ok := request.(*messages.PingRequest); ok {
			reply = handlePingRequest(v)
		}

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any errors that may have occurred
	// in the switch block or if the message could not
	// be cast to a specific type
	if (err != nil) || (reflect.ValueOf(reply).IsNil()) {
		return err
	}

	// send the reply as an http.Request back to the Neon.Cadence Library
	// via http.PUT
	var replyMessage messages.IProxyMessage = reply.GetProxyMessage()
	resp, err := putToNeonCadenceClient(replyMessage)
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
// IProxyRequest message type handler methods

func handleCancelRequest(request *messages.CancelRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("CancelRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	if v, ok := reply.(*messages.CancelReply); ok {
		buildCancelReply(v, nil, true)
	}

	return reply
}

func handleConnectRequest(request *messages.ConnectRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ConnectRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new ConnectReply
	reply := createReplyMessage(request)

	// set endpoint to cadence cluster
	// and identity
	// set cadenceClientTimeout
	endpoints := *request.GetEndpoints()
	identity := *request.GetIdentity()
	cadenceClientTimeout = request.GetClientTimeout()

	// client options
	opts := client.Options{
		Identity: identity,
	}

	// setup the CadenceClientHelper
	clientHelper = cadenceclient.NewCadenceClientHelper()
	clientHelper.SetHostPort(endpoints)
	clientHelper.SetClientOptions(&opts)

	err := clientHelper.SetupServiceConfig()
	if err != nil {

		// set the client helper to nil indicating that
		// there is no connection that has been made to the cadence
		// server
		clientHelper = nil

		// build the rest of the reply with a custom error
		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}
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

		// build the domain client using a configured CadenceClientHelper instance
		domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
		if err != nil {
			connectChan <- err
			return
		}

		// send a describe domain request to the cadence server
		_, err = domainClient.Describe(ctx, _cadenceSystemDomain)
		if err != nil {
			connectChan <- err
			return
		}

		connectChan <- nil
	}()

	connectResult := <-connectChan
	if connectResult != nil {
		clientHelper = nil

		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	if v, ok := reply.(*messages.ConnectReply); ok {
		buildConnectReply(v, nil)
	}

	return reply
}

func handleDomainDescribeRequest(request *messages.DomainDescribeRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainDescribeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new DomainDescribeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// send a describe domain request to the cadence server
	describeDomainResponse, err := domainClient.Describe(context.Background(), *request.GetName())
	if err != nil {
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	if v, ok := reply.(*messages.DomainDescribeReply); ok {
		buildDomainDescribeReply(v, nil, describeDomainResponse)
	}

	return reply
}

func handleDomainRegisterRequest(request *messages.DomainRegisterRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new DomainRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// create a new cadence domain RegisterDomainRequest for
	// registering a new domain
	emitMetrics := request.GetEmitMetrics()
	retentionDays := request.GetRetentionDays()
	domainRegisterRequest := cadenceshared.RegisterDomainRequest{
		Name:                                   request.GetName(),
		Description:                            request.GetDescription(),
		OwnerEmail:                             request.GetOwnerEmail(),
		EmitMetric:                             &emitMetrics,
		WorkflowExecutionRetentionPeriodInDays: &retentionDays,
	}

	// register the domain using the RegisterDomainRequest
	err = domainClient.Register(context.Background(), &domainRegisterRequest)
	if err != nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully registered", zap.String("Domain Name", domainRegisterRequest.GetName()))

	if v, ok := reply.(*messages.DomainRegisterReply); ok {
		buildDomainRegisterReply(v, nil)
	}

	return reply
}

func handleDomainUpdateRequest(request *messages.DomainUpdateRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainUpdateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new DomainUpdateReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

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
		Name:          request.GetName(),
		Configuration: configuration,
		UpdatedInfo:   updatedInfo,
	}

	// Update the domain using the UpdateDomainRequest
	err = domainClient.Update(context.Background(), &domainUpdateRequest)
	if err != nil {

		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully Updated", zap.String("Domain Name", domainUpdateRequest.GetName()))

	if v, ok := reply.(*messages.DomainUpdateReply); ok {
		buildDomainUpdateReply(v, nil)
	}

	return reply
}

func handleHeartbeatRequest(request *messages.HeartbeatRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("HeartbeatRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new HeartbeatReply
	reply := createReplyMessage(request)
	if v, ok := reply.(*messages.HeartbeatReply); ok {
		buildHeartbeatReply(v, nil)
	}

	return reply
}

func handleInitializeRequest(request *messages.InitializeRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// get the port and address from the InitializeRequest
	address := *request.GetLibraryAddress()
	port := request.GetLibraryPort()
	replyAddress = fmt.Sprintf("http://%s:%d/",
		address,
		port,
	)

	// $debug(jack.burns): DELETE THIS!
	if debugPrelaunch {
		replyAddress = "http://127.0.0.2:5001/"
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest info",
		zap.String("Library Address", address),
		zap.Int32("LibaryPort", port),
		zap.String("Reply Address", replyAddress),
	)

	if v, ok := reply.(*messages.InitializeReply); ok {
		buildInitializeReply(v, nil)
	}

	return reply
}

func handleTerminateRequest(request *messages.TerminateRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("TerminateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new TerminateReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true

	if v, ok := reply.(*messages.TerminateReply); ok {
		buildTerminateReply(v, nil)
	}

	return reply
}

func handleWorkflowRegisterRequest(request *messages.WorkflowRegisterRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowRegisterRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
			buildWorkflowRegisterReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// define some variables to hold workflow return values
	workflowContextID := cadenceworkflows.NextWorkflowContextID()

	// create workflow function
	workflowFunc := func(ctx workflow.Context, input []byte) ([]byte, error) {
		wectx := new(cadenceworkflows.WorkflowExecutionContext)
		wectx.SetContext(ctx)

		// set the WorkflowExecutionContext in the
		// WorkflowExecutionContexts
		workflowContextID = cadenceworkflows.WorkflowExecutionContexts.Add(workflowContextID, wectx)

		// Send a WorkflowInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		requestID := NextRequestID()
		workflowInvokeRequest := messages.NewWorkflowInvokeRequest()
		workflowInvokeRequest.SetRequestID(requestID)
		workflowInvokeRequest.SetArgs(input)
		workflowInvokeRequest.SetWorkflowContextID(workflowContextID)

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
		op := NewOperation(workflowInvokeRequest.GetRequestID(), workflowInvokeRequest)
		future, settable := workflow.NewFuture(ctx)
		op.SetFuture(future)
		op.SetSettable(settable)
		Operations.Add(requestID, op)

		// send the request to the Neon.Cadence client
		go func() {

			// send the WorkflowInvokeRequest
			var message messages.IProxyMessage = workflowInvokeRequest.GetProxyMessage()
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
		}()

		// wait for the future to be unblocked
		var result []byte
		if err := future.Get(ctx, &result); err != nil {
			return nil, err
		}

		return result, nil
	}

	// register the workflow
	// build the reply
	workflow.RegisterWithOptions(workflowFunc, workflow.RegisterOptions{Name: *request.GetName()})
	if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
		buildWorkflowRegisterReply(v, nil)
	}

	return reply
}

func handleWorkflowExecuteRequest(request *messages.WorkflowExecuteRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowExecuteRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowExecuteReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// grab the client.ExecuteWorkflow parameters
	opts := request.GetOptions()
	args := request.GetArgs()
	workflowName := request.GetWorkflow()

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signalwithstart the specified workflow
	workflowRun, err := client.ExecuteWorkflow(ctx, *opts, *workflowName, args)
	if err != nil {
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// extract the workflow ID and RunID
	workflowExecution := new(workflow.Execution)
	workflowExecution.ID = workflowRun.GetID()
	workflowExecution.RunID = workflowRun.GetRunID()
	if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
		buildWorkflowExecuteReply(v, nil, workflowExecution)
	}

	return reply
}

func handleWorkflowInvokeRequest(request *messages.WorkflowInvokeRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("not implemented exception for message type WorkflowInvokeRequest")
	return nil

}

func handleNewWorkerRequest(request *messages.NewWorkerRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("NewWorkerRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new NewWorkerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			buildNewWorkerReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// gather the worker.New() parameters
	// create a new worker using a configured CadenceClientHelper instance
	domain := request.GetDomain()
	taskList := request.GetTaskList()
	opts := request.GetOptions()
	worker := worker.New(clientHelper.Service, *domain, *taskList, *opts)

	// put the worker and workerID from the new worker to the
	// WorkersMap and then run the worker by calling Run() on it
	workerID := cadenceworkers.WorkersMap.Add(cadenceworkers.NextWorkerID(), worker)
	err := worker.Run()
	if err != nil {
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			buildNewWorkerReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom), workerID)
		}

		return reply
	}

	if v, ok := reply.(*messages.NewWorkerReply); ok {
		buildNewWorkerReply(v, nil, workerID)
	}

	return reply
}

func handleStopWorkerRequest(request *messages.StopWorkerRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("StopWorkerRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new StopWorkerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			buildStopWorkerReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// get the workerID from the request so that we know
	// what worker to stop
	workerID := request.GetWorkerID()
	worker := cadenceworkers.WorkersMap.Get(workerID)
	if worker == nil {
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			buildStopWorkerReply(v, cadenceerrors.NewCadenceError(
				entityNotExistError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// stop the worker and
	// remove it from the cadenceworkers.WorkersMap
	worker.Stop()
	workerID = cadenceworkers.WorkersMap.Remove(workerID)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Worker has been deleted", zap.Int64("WorkerID", workerID))

	if v, ok := reply.(*messages.StopWorkerReply); ok {
		buildStopWorkerReply(v, nil)
	}

	return reply
}

func handlePingRequest(request *messages.PingRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("PingRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new PingReply
	reply := createReplyMessage(request)

	if v, ok := reply.(*messages.PingReply); ok {
		buildPingReply(v, nil)
	}

	return reply
}

func handleWorkflowCancelRequest(request *messages.WorkflowCancelRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowCancelRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowCancelReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// grab the client.CancelWorkflow parameters and
	workflowID := request.GetWorkflowID()
	runID := request.GetRunID()

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// cancel the specified workflow
	err = client.CancelWorkflow(ctx, *workflowID, *runID)
	if err != nil {
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	if v, ok := reply.(*messages.WorkflowCancelReply); ok {
		buildWorkflowCancelReply(v, nil)
	}

	return reply
}

func handleWorkflowTerminateRequest(request *messages.WorkflowTerminateRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowTerminateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowTerminateReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// grab the client.TerminateWorkflow parameters and
	workflowID := request.GetWorkflowID()
	runID := request.GetRunID()
	reason := request.GetReason()
	details := request.GetDetails()

	// create the context to terminate the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// terminate the specified workflow
	err = client.TerminateWorkflow(ctx, *workflowID, *runID, *reason, details)
	if err != nil {
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
		buildWorkflowTerminateReply(v, nil)
	}

	return reply
}

func handleWorkflowSignalRequest(request *messages.WorkflowSignalRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSignalReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// grab the client.SignalWorkflow parameters
	workflowID := request.GetWorkflowID()
	runID := request.GetRunID()
	signalName := request.GetSignalName()
	signalArgs := request.GetSignalArgs()

	// create the context to signal the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signal the specified workflow
	err = client.SignalWorkflow(ctx, *workflowID, *runID, *signalName, signalArgs)
	if err != nil {
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	if v, ok := reply.(*messages.WorkflowSignalReply); ok {
		buildWorkflowSignalReply(v, nil)
	}

	return reply
}

func handleWorkflowSignalWithStartRequest(request *messages.WorkflowSignalWithStartRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalWithStartRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSignalWithStartReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// grab the client.SignalWithStartWorkflow parameters
	workflowID := request.GetWorkflowID()
	signalName := request.GetSignalName()
	signalArgs := request.GetSignalArgs()
	opts := request.GetOptions()
	workflowArgs := request.GetWorkflowArgs()
	workflow := request.GetWorkflow()

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signalwithstart the specified workflow
	workflowExecution, err := client.SignalWithStartWorkflow(ctx, *workflowID, *signalName, signalArgs, *opts, *workflow, workflowArgs)
	if err != nil {
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
		buildWorkflowSignalWithStartReply(v, nil, workflowExecution)
	}

	return reply
}

func handleWorkflowSetCacheSizeRequest(request *messages.WorkflowSetCacheSizeRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSetCacheSizeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSetCacheSizeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
			buildWorkflowSetCacheSizeReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// set the sticky workflow cache size
	worker.SetStickyWorkflowCacheSize(request.GetSize())
	if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
		buildWorkflowSetCacheSizeReply(v, nil)
	}

	return reply
}

func handleWorkflowQueryRequest(request *messages.WorkflowQueryRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowQueryRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowQueryReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// get the properties from the request
	workflowID := request.GetWorkflowID()
	runID := request.GetRunID()
	queryName := request.GetQueryName()
	queryArgs := request.GetQueryArgs()

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// query the workflow via the cadence client
	value, err := client.QueryWorkflow(ctx, *workflowID, *runID, *queryName, queryArgs)
	if err != nil {
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// extract the result
	var result []byte
	if value.HasValue() {
		err = value.Get(&result)
		if err != nil {
			if v, ok := reply.(*messages.WorkflowQueryReply); ok {
				buildWorkflowQueryReply(v, cadenceerrors.NewCadenceError(
					err.Error(),
					cadenceerrors.Custom))
			}

			return reply
		}
	}

	if v, ok := reply.(*messages.WorkflowQueryReply); ok {
		buildWorkflowQueryReply(v, nil, result)
	}

	return reply
}

func handleWorkflowMutableRequest(request *messages.WorkflowMutableRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowMutableRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowMutableReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			buildWorkflowMutableReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// get the WorkflowContextID and the corresponding context
	workflowContextID := request.GetWorkflowContextID()
	wectx := cadenceworkflows.WorkflowExecutionContexts.Get(workflowContextID)
	if wectx == nil {
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			buildWorkflowMutableReply(v, cadenceerrors.NewCadenceError(
				entityNotExistError.Error(),
				cadenceerrors.Custom))
		}

		return reply
	}

	// f function for workflow.MutableSideEffect
	mutableID := request.GetMutableID()
	f := func(ctx workflow.Context) interface{} {
		requestID := NextRequestID()
		workflowMutableInvokeRequest := messages.NewWorkflowMutableInvokeRequest()
		workflowMutableInvokeRequest.SetRequestID(requestID)
		workflowMutableInvokeRequest.SetWorkflowContextID(workflowContextID)
		workflowMutableInvokeRequest.SetMutableID(mutableID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(workflowMutableInvokeRequest.GetRequestID(), workflowMutableInvokeRequest)
		future, settable := workflow.NewFuture(ctx)
		op.SetFuture(future)
		op.SetSettable(settable)
		Operations.Add(requestID, op)

		// create channel for WorkflowMutableInvokeReply and add it
		// to the mutableChannelsMap
		go func() {

			// send the request to the Neon.Cadence Lib client
			// send the WorkflowInvokeRequest
			var message messages.IProxyMessage = workflowMutableInvokeRequest.GetProxyMessage()
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
		}()

		// wait for the future to be unblocked
		var result []byte
		if err := future.Get(ctx, &result); err != nil {
			return err
		}

		return result
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

	// execute the cadence server SideEffectMutable call
	sideEffectValue := workflow.MutableSideEffect(wectx.GetContext(), *mutableID, f, equals)

	// extract the result
	var result interface{}
	if sideEffectValue.HasValue() {
		err := sideEffectValue.Get(&result)

		// check the error of retreiving the value
		if err != nil {
			if v, ok := reply.(*messages.WorkflowMutableReply); ok {
				buildWorkflowMutableReply(v, cadenceerrors.NewCadenceError(
					err.Error(),
					cadenceerrors.Custom))
			}

			return reply
		}

		// check if the result is a cadenceerrors.CadenceError or
		// a []byte result
		if v, ok := result.(*cadenceerrors.CadenceError); ok {
			if _v, _ok := reply.(*messages.WorkflowMutableReply); _ok {
				buildWorkflowMutableReply(_v, v)
			}
		} else if v, ok := result.([]byte); ok {
			if _v, _ok := reply.(*messages.WorkflowMutableReply); _ok {
				buildWorkflowMutableReply(_v, nil, v)
			}
		}
	}

	return reply
}

func handleWorkflowMutableInvokeRequest(request *messages.WorkflowMutableInvokeRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowMutableInvokeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowMutableInvokeReply
	reply := createReplyMessage(request)

	if v, ok := reply.(*messages.WorkflowMutableInvokeReply); ok {
		buildWorkflowMutableInvokeReply(v, nil)
	}

	return reply
}

// -------------------------------------------------------------------------
// Helpers for sending ProxyReply messages back to Neon.Cadence Library

func createReplyMessage(request messages.IProxyRequest) messages.IProxyMessage {

	// get the correct reply type and initialize a new
	// reply corresponding to the request message type
	var proxyMessage messages.IProxyMessage = messages.CreateNewTypedMessage(request.GetReplyType())
	if reflect.ValueOf(proxyMessage).IsNil() {
		return nil
	}
	proxyMessage.SetRequestID(request.GetRequestID())

	return proxyMessage
}

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {

	// serialize the message
	proxyMessage := message.GetProxyMessage()
	content, err := proxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, replyAddress, buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error creating Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	// set the request header to specified content type
	req.Header.Set("Content-Type", ContentType)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Neon.Cadence Library request",
		zap.String("Request Address", req.URL.String()),
		zap.String("Request Content-Type", req.Header.Get("Content-Type")),
		zap.String("Request Method", req.Method),
	)

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error sending Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	return resp, nil
}
