package endpoints

import (
	"bytes"
	"context"
	"fmt"
	"net/http"
	"os"
	"reflect"

	cadenceclient "github.com/loopieio/cadence-proxy/internal/cadenceclient"
	domain "github.com/loopieio/cadence-proxy/internal/cadencedomain"
	"github.com/loopieio/cadence-proxy/internal/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
	"github.com/loopieio/cadence-proxy/internal/messages/types"
	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/client"
	"go.uber.org/zap"
)

func createReplyMessage(request types.IProxyRequest) types.IProxyMessage {

	// get the correct reply type and initialize a new
	// reply corresponding to the request message type
	proxyMessage := types.CreateNewTypedMessage(request.GetReplyType())
	if reflect.ValueOf(proxyMessage).IsNil() {
		return nil
	}
	proxyMessage.SetRequestID(request.GetRequestID())

	return proxyMessage
}

func putReply(content []byte, address string) (*http.Response, error) {

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

// -------------------------------------------------------------------------
// IProxyRequest message type handlers

func handleIProxyRequest(request types.IProxyRequest, typeCode messages.MessageType) error {

	// error for catching exceptions in the switch block
	var err error
	var reply types.IProxyMessage

	// handle the messages individually based on their message type
	switch typeCode {

	// InitializeRequest
	case messages.InitializeRequest:
		if v, ok := request.(*types.InitializeRequest); ok {
			reply, err = handleInitializeRequest(v)
		}

	// HeartbeatRequest
	case messages.HeartbeatRequest:
		if v, ok := request.(*types.HeartbeatRequest); ok {
			reply, err = handleHeartbeatRequest(v)
		}

	// CancelRequest
	case messages.CancelRequest:
		if v, ok := request.(*types.CancelRequest); ok {
			reply, err = handleCancelRequest(v)
		}

	// ConnectRequest
	case messages.ConnectRequest:
		if v, ok := request.(*types.ConnectRequest); ok {
			reply, err = handleConnectRequest(v)
		}

	// DomainDescribeRequest
	case messages.DomainDescribeRequest:
		if v, ok := request.(*types.DomainDescribeRequest); ok {
			reply, err = handleDomainDescribeRequest(v)
		}

	// DomainRegisterRequest
	case messages.DomainRegisterRequest:
		if v, ok := request.(*types.DomainRegisterRequest); ok {
			reply, err = handleDomainRegisterRequest(v)
		}

	// DomainUpdateRequest
	case messages.DomainUpdateRequest:
		if v, ok := request.(*types.DomainUpdateRequest); ok {
			reply, err = handleDomainUpdateRequest(v)
		}

	// TerminateRequest
	case messages.TerminateRequest:
		if v, ok := request.(*types.TerminateRequest); ok {
			reply, err = handleTerminateRequest(v)
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

	// Get the pointer to the ProxyMessage
	replyProxyMessage := reply.GetProxyMessage()

	// serialize the reply message into a []byte
	// to send back over the network
	serializedMessage, err := replyProxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return err
	}

	// send the reply as an http.Request back to the Neon.Cadence Library
	// via http.PUT
	resp, err := putReply(serializedMessage, replyAddress)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	return nil
}

func handleActivityRequest(request *types.ActivityRequest) (types.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type ActivityRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityRequest", zap.Error(err))
	return nil, err

}

func handleCancelRequest(request *types.CancelRequest) (types.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("CancelRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*types.CancelReply); ok {
			buildCancelReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := cadenceclient.ClientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*types.CancelReply); ok {
			buildCancelReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	targetRequestID := request.GetTargetRequestID()
	err = client.CancelWorkflow(context.Background(), string(targetRequestID), "")
	if err != nil {
		if v, ok := reply.(*types.CancelReply); ok {
			buildCancelReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom), false)
		}

		return reply, nil
	}

	if v, ok := reply.(*types.CancelReply); ok {
		buildCancelReply(v, nil, true)
	}

	return reply, nil
}

func handleConnectRequest(request *types.ConnectRequest) (types.IProxyMessage, error) {

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
		if v, ok := reply.(*types.ConnectReply); ok {
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

		if v, ok := reply.(*types.ConnectReply); ok {
			buildConnectReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	if v, ok := reply.(*types.ConnectReply); ok {
		buildConnectReply(v, nil)
	}

	return reply, nil
}

func handleDomainDescribeRequest(request *types.DomainDescribeRequest) (types.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainDescribeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*types.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*types.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// send a describe domain request to the cadence server
	describeDomainResponse, err := domainClient.Describe(context.Background(), *request.GetName())
	if err != nil {
		if v, ok := reply.(*types.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	if v, ok := reply.(*types.DomainDescribeReply); ok {
		buildDomainDescribeReply(v, nil, describeDomainResponse)
	}

	return reply, nil
}

func handleDomainRegisterRequest(request *types.DomainRegisterRequest) (types.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*types.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*types.DomainRegisterReply); ok {
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
		if v, ok := reply.(*types.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully registered", zap.String("Domain Name", domainRegisterRequest.GetName()))

	if v, ok := reply.(*types.DomainRegisterReply); ok {
		buildDomainRegisterReply(v, nil)
	}

	return reply, nil
}

func handleDomainUpdateRequest(request *types.DomainUpdateRequest) (types.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainUpdateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// create the reply to Neon.Cadence library
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*types.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				connectionError.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*types.DomainRegisterReply); ok {
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

		if v, ok := reply.(*types.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom))
		}

		return reply, nil
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully Updated", zap.String("Domain Name", domainUpdateRequest.GetName()))

	if v, ok := reply.(*types.DomainUpdateReply); ok {
		buildDomainUpdateReply(v, nil)
	}

	return reply, nil
}

func handleHeartbeatRequest(request *types.HeartbeatRequest) (types.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("HeartbeatRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new HeartbeatReply
	reply := createReplyMessage(request)
	if v, ok := reply.(*types.HeartbeatReply); ok {
		buildHeartbeatReply(v, nil)
	}

	return reply, nil
}

func handleInitializeRequest(request *types.InitializeRequest) (types.IProxyMessage, error) {

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

	if v, ok := reply.(*types.InitializeReply); ok {
		buildInitializeReply(v, nil)
	}

	return reply, nil
}

func handleTerminateRequest(request *types.TerminateRequest) (types.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("TerminateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true

	if v, ok := reply.(*types.TerminateReply); ok {
		buildTerminateReply(v, nil)
	}

	return reply, nil
}

// -------------------------------------------------------------------------
// ProxyReply builders

func buildCancelReply(reply *types.CancelReply, cadenceError *cadenceerrors.CadenceError, wasCancelled ...bool) {
	reply.SetError(cadenceError)

	if len(wasCancelled) > 0 {
		reply.SetWasCancelled(wasCancelled[0])
	}
}

func buildConnectReply(reply *types.ConnectReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainDescribeReply(reply *types.DomainDescribeReply, cadenceError *cadenceerrors.CadenceError, describeDomainResponse ...*cadenceshared.DescribeDomainResponse) {
	reply.SetError(cadenceError)

	if len(describeDomainResponse) > 0 {
		d := describeDomainResponse[0]
		reply.SetDomainInfoName(d.DomainInfo.Name)
		reply.SetDomainInfoDescription(d.DomainInfo.Description)

		domainStatus := domain.DomainStatus(int(*d.DomainInfo.Status))
		reply.SetDomainInfoStatus(&domainStatus)
		reply.SetConfigurationEmitMetrics(*d.Configuration.EmitMetric)
		reply.SetConfigurationRetentionDays(*d.Configuration.WorkflowExecutionRetentionPeriodInDays)
		reply.SetDomainInfoOwnerEmail(d.DomainInfo.OwnerEmail)
	}
}

func buildDomainRegisterReply(reply *types.DomainRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainUpdateReply(reply *types.DomainUpdateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildHeartbeatReply(reply *types.HeartbeatReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildInitializeReply(reply *types.InitializeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildTerminateReply(reply *types.TerminateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}
