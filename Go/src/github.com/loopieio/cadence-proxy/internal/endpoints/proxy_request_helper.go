package endpoints

import (
	"bytes"
	"context"
	"fmt"
	"net/http"
	"os"
	"reflect"

	"go.uber.org/cadence/workflow"

	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceworkflows"

	"go.uber.org/cadence/worker"

	cadenceclient "github.com/loopieio/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/client"
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
	defer resp.Body.Close()

	return nil
}

// -------------------------------------------------------------------------
// IProxyRequest message type handler methods

func handleActivityRequest(request *messages.ActivityRequest) messages.IProxyMessage {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("not implemented exception for message type ActivityRequest")
	return nil

}

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
	endpoints := *request.GetEndpoints()
	identity := *request.GetIdentity()

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
	ctx, cancel := context.WithTimeout(context.Background(), _cadenceTimeout)

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
		future, settable := workflow.NewFuture(ctx)
		wectx := new(cadenceworkflows.WorkflowExecutionContext)
		wectx.SetContext(ctx)
		wectx.SetFuture(future)
		wectx.SetSettable(settable)

		// set the WorkflowExecutionContext in the
		// WorkflowExecutionContextsMap
		workflowContextID = cadenceworkflows.WorkflowExecutionContextsMap.Add(workflowContextID, wectx)

		// Send a WorkflowInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		workflowInvokeRequest := messages.NewWorkflowInvokeRequest()
		workflowInvokeRequest.SetRequestID(NextRequestID())
		workflowInvokeRequest.SetArgs(input)
		workflowInvokeRequest.SetWorkflowContextID(workflowContextID)

		// send the WorkflowInvokeRequest
		var message messages.IProxyMessage = workflowInvokeRequest.GetProxyMessage()
		resp, err := putToNeonCadenceClient(message)
		if err != nil {
			return nil, err
		}
		defer resp.Body.Close()

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Checking if Future is ready", zap.Bool("Future IsReady", wectx.IsReady()))

		// wait for the future to be unblocked
		var result []byte
		if err = future.Get(ctx, &result); err != nil {
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
	logger.Debug("not implemented exception for message type WorkflowExecuteRequest")
	return nil

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
	workerID = cadenceworkers.WorkersMap.Delete(workerID)
	if v, ok := reply.(*messages.StopWorkerReply); ok {
		buildStopWorkerReply(v, nil)
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
