package endpoints

import (
	"bytes"
	"context"
	"fmt"
	"net/http"
	"os"
	"reflect"

	cadenceclient "github.com/loopieio/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/client"
	"go.uber.org/zap"
)

// -------------------------------------------------------------------------
// IProxyRequest message type handlers

func handleIProxyRequest(request messages.IProxyRequest, typeCode messagetypes.MessageType) error {

	// error for catching exceptions in the switch block
	var err error
	var reply messages.IProxyMessage

	// handle the messages individually based on their message type
	switch typeCode {

	// InitializeRequest
	case messagetypes.InitializeRequest:
		if v, ok := request.(*messages.InitializeRequest); ok {
			reply, err = handleInitializeRequest(v)
		}

	// HeartbeatRequest
	case messagetypes.HeartbeatRequest:
		if v, ok := request.(*messages.HeartbeatRequest); ok {
			reply, err = handleHeartbeatRequest(v)
		}

	// CancelRequest
	case messagetypes.CancelRequest:
		if v, ok := request.(*messages.CancelRequest); ok {
			reply, err = handleCancelRequest(v)
		}

	// ConnectRequest
	case messagetypes.ConnectRequest:
		if v, ok := request.(*messages.ConnectRequest); ok {
			reply, err = handleConnectRequest(v)
		}

	// DomainDescribeRequest
	case messagetypes.DomainDescribeRequest:
		if v, ok := request.(*messages.DomainDescribeRequest); ok {
			reply, err = handleDomainDescribeRequest(v)
		}

	// DomainRegisterRequest
	case messagetypes.DomainRegisterRequest:
		if v, ok := request.(*messages.DomainRegisterRequest); ok {
			reply, err = handleDomainRegisterRequest(v)
		}

	// DomainUpdateRequest
	case messagetypes.DomainUpdateRequest:
		if v, ok := request.(*messages.DomainUpdateRequest); ok {
			reply, err = handleDomainUpdateRequest(v)
		}

	// TerminateRequest
	case messagetypes.TerminateRequest:
		if v, ok := request.(*messages.TerminateRequest); ok {
			reply, err = handleTerminateRequest(v)
		}

	// WorkflowRegisterRequest
	case messagetypes.WorkflowRegisterRequest:
		if v, ok := request.(*messages.WorkflowRegisterRequest); ok {
			reply, err = handleWorkflowRegisterRequest(v)
		}

	// WorkflowExecuteRequest
	case messagetypes.WorkflowExecuteRequest:
		if v, ok := request.(*messages.WorkflowExecuteRequest); ok {
			reply, err = handleWorkflowExecuteRequest(v)
		}

	// WorkflowInvokeRequest
	case messagetypes.WorkflowInvokeRequest:
		if v, ok := request.(*messages.WorkflowInvokeRequest); ok {
			reply, err = handleWorkflowInvokeRequest(v)
		}

	// NewWorkerRequest
	case messagetypes.NewWorkerRequest:
		if v, ok := request.(*messages.NewWorkerRequest); ok {
			reply, err = handleNewWorkerRequest(v)
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

	// serialize the reply message into a []byte
	// to send back over the network
	replyProxyMessage := reply.GetProxyMessage()
	serializedMessage, err := replyProxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return err
	}

	// send the reply as an http.Request back to the Neon.Cadence Library
	// via http.PUT
	resp, err := putReply(serializedMessage)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	return nil
}

func handleActivityRequest(request *messages.ActivityRequest) (messages.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type ActivityRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityRequest", zap.Error(err))
	return nil, err

}

func handleCancelRequest(request *messages.CancelRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("CancelRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	if v, ok := reply.(*messages.CancelReply); ok {
		buildCancelReply(v, nil, true)
	}

	return reply, nil
}

func handleConnectRequest(request *messages.ConnectRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ConnectRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
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
	cadenceclient.ClientHelper = cadenceclient.NewCadenceClientHelper()
	cadenceclient.ClientHelper.SetHostPort(endpoints)
	cadenceclient.ClientHelper.SetClientOptions(&opts)

	err := cadenceclient.ClientHelper.SetupServiceConfig()
	if err != nil {

		// set the client helper to nil indicating that
		// there is no connection that has been made to the cadence
		// server
		cadenceclient.ClientHelper = nil

		// build the rest of the reply with a custom error
		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}
	}

	// make a channel that waits for a connection to be established until returning ready
	connectChan := make(chan error)
	ctx, cancel := context.WithTimeout(context.Background(), _cadenceTimeout)
	defer cancel()

	go func() {

		// build the domain client using a configured CadenceClientHelper instance
		domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
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
		cadenceclient.ClientHelper = nil

		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	if v, ok := reply.(*messages.ConnectReply); ok {
		buildConnectReply(v, nil)
	}

	return reply, nil
}

func handleDomainDescribeRequest(request *messages.DomainDescribeRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainDescribeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// send a describe domain request to the cadence server
	describeDomainResponse, err := domainClient.Describe(context.Background(), *request.GetName())
	if err != nil {
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	if v, ok := reply.(*messages.DomainDescribeReply); ok {
		buildDomainDescribeReply(v, nil, describeDomainResponse)
	}

	return reply, nil
}

func handleDomainRegisterRequest(request *messages.DomainRegisterRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
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

		return reply, nil
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully registered", zap.String("Domain Name", domainRegisterRequest.GetName()))

	if v, ok := reply.(*messages.DomainRegisterReply); ok {
		buildDomainRegisterReply(v, nil)
	}

	return reply, nil
}

func handleDomainUpdateRequest(request *messages.DomainUpdateRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainUpdateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// create the reply to Neon.Cadence library
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
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

		return reply, nil
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully Updated", zap.String("Domain Name", domainUpdateRequest.GetName()))

	if v, ok := reply.(*messages.DomainUpdateReply); ok {
		buildDomainUpdateReply(v, nil)
	}

	return reply, nil
}

func handleHeartbeatRequest(request *messages.HeartbeatRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("HeartbeatRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new HeartbeatReply
	reply := createReplyMessage(request)
	if v, ok := reply.(*messages.HeartbeatReply); ok {
		buildHeartbeatReply(v, nil)
	}

	return reply, nil
}

func handleInitializeRequest(request *messages.InitializeRequest) (messages.IProxyMessage, error) {

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

	return reply, nil
}

func handleTerminateRequest(request *messages.TerminateRequest) (messages.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("TerminateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true

	if v, ok := reply.(*messages.TerminateReply); ok {
		buildTerminateReply(v, nil)
	}

	return reply, nil
}

func handleWorkflowRegisterRequest(request *messages.WorkflowRegisterRequest) (messages.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type WorkflowRegisterRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowRegisterRequest", zap.Error(err))
	return nil, err

}

func handleWorkflowExecuteRequest(request *messages.WorkflowExecuteRequest) (messages.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type WorkflowExecuteRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowExecuteRequest", zap.Error(err))
	return nil, err

}

func handleWorkflowInvokeRequest(request *messages.WorkflowInvokeRequest) (messages.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type WorkflowInvokeRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowInvokeRequest", zap.Error(err))
	return nil, err

}

func handleNewWorkerRequest(request *messages.NewWorkerRequest) (messages.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type NewWorkerRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling NewWorkerRequest", zap.Error(err))
	return nil, err

}

func handleStopWorkerRequest(request *messages.StopWorkerRequest) (messages.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type StopWorkerRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling StopWorkerRequest", zap.Error(err))
	return nil, err
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

func putReply(content []byte) (*http.Response, error) {

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
