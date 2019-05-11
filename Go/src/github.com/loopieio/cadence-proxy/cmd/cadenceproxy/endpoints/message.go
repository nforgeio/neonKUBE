package endpoints

import (
	"bytes"
	"context"
	"fmt"
	"io/ioutil"
	"net/http"
	"os"
	"reflect"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceerrors"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/client"
	"go.uber.org/zap"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
	domain "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadencedomain"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/activity"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/cluster"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/workflow"
)

const (

	// deathClockThreshold is the limit of critical failures
	// returned by calls to the cadence server indicating a connection
	// issue
	deathClockThreshold = 5
)

var (

	// replyAddress specifies the address that the Neon.Cadence library
	// will be listening on for replies from the cadence proxy
	replyAddress string

	// terminate is a boolean that will be set after handling an incoming
	// TerminateRequest.  A true value will indicate that the server instance
	// needs to gracefully shut down after handling the request, and a false value
	// indicates the server continues to run
	terminate bool

	// INTERNAL USE ONLY:</b> Optionally indicates that the <b>cadence-client</b>
	// will not perform the <see cref="InitializeRequest"/>/<see cref="InitializeReply"/>
	// and <see cref="TerminateRequest"/>/<see cref="TerminateReply"/> handshakes
	// with the <b>cadence-proxy</b> for debugging purposes.  This defaults to
	// <c>false</c>
	debugPrelaunch = false

	// deathClock is an accumulator that tallies errors thrown by cadence that might
	// indicate that the connection to the cadence server has been compromised.
	// This value will be checked upon recieving a HeartbeatRequest, and if it has
	// reached a specified threshold, then add a CadenceError to the HeartbeatReply.
	// This will tell the Neon.Cadence library to shut the cadence-proxy down
	deathClock int
)

// MessageHandler accepts an http.PUT requests and parses the
// request body, converts it into an ProxyMessage object
// and talks through the uber cadence client to the cadence server,
// executing the instructions incoded in the request.
//
// param w http.ResponseWriter
//
// param r *http.Request
func MessageHandler(w http.ResponseWriter, r *http.Request) {

	// grab the logger from the server instance
	logger = Instance.Logger

	// check if the request has the correct content type
	// and is an http.PUT request
	statusCode, err := checkRequestValidity(w, r)
	if err != nil {
		http.Error(w, err.Error(), statusCode)
	}

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(r.Body)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Null request body", zap.String("Error", err.Error()))

		// write the error and status code into response
		http.Error(w, err.Error(), http.StatusBadRequest)
		panic(err)
	}

	statusCode, err = proccessIncomingMessage(payload)
	if err != nil {

		// write the error and status code into response
		http.Error(w, err.Error(), statusCode)
		panic(err)
	}

	// write status code as response back to sender
	w.WriteHeader(http.StatusOK)

	// check to see if terminate is true, if it is then gracefully
	// shut down the server instance by sending a truth bool value
	// to the instance's ShutdownChannel
	if terminate {
		Instance.ShutdownChannel <- true
	}
}

// -------------------------------------------------------------------------
// Helper methods for handling incoming messages

func proccessIncomingMessage(payload []byte) (int, error) {

	// deserialize the payload
	buf := bytes.NewBuffer(payload)

	// new IProxyMessage to deserialize the request body into
	message, err := base.Deserialize(buf, false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error deserializing input", zap.Error(err))
		return http.StatusBadRequest, err
	}

	// typecode to get the specific message type
	typeCode := message.GetProxyMessage().Type

	// determine whether the input request is a ProxyReply or ProxyRequest
	switch s := message.(type) {

	// Nil type value
	case nil:
		err := fmt.Errorf("nil type for incoming ProxyMessage: %v of type %v", message, typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error processing incoming message", zap.Error(err))
		return http.StatusBadRequest, err

	// IProxyRequest
	case base.IProxyRequest:
		return handleIProxyRequest(s, typeCode)

	// IProxyReply
	case base.IProxyReply:
		return handleIProxyReply(s, typeCode)

	// Unrecognized type
	default:
		err := fmt.Errorf("unhandled message type. could not complete type assertion for type %v", typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error processing incoming message", zap.Error(err))
		return http.StatusBadRequest, err
	}
}

func createReplyMessage(request base.IProxyRequest) base.IProxyMessage {

	// get the correct reply type and initialize a new
	// reply corresponding to the request message type
	key := int(request.GetReplyType())
	proxyMessage := base.MessageTypeStructMap[key].Clone()
	if reflect.ValueOf(proxyMessage).IsNil() {
		return nil
	}

	// set the requestId
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

func handleIProxyRequest(request base.IProxyRequest, typeCode messages.MessageType) (int, error) {

	// error for catching exceptions in the switch block
	var err error
	var reply base.IProxyMessage

	// handle the messages individually based on their message type
	switch typeCode {

	// InitializeRequest
	case messages.InitializeRequest:
		if v, ok := request.(*cluster.InitializeRequest); ok {
			reply, err = handleInitializeRequest(v)
		}

	// HeartbeatRequest
	case messages.HeartbeatRequest:
		if v, ok := request.(*cluster.HeartbeatRequest); ok {
			reply, err = handleHeartbeatRequest(v)
		}

	// CancelRequest
	case messages.CancelRequest:
		if v, ok := request.(*cluster.CancelRequest); ok {
			reply, err = handleCancelRequest(v)
		}

	// ConnectRequest
	case messages.ConnectRequest:
		if v, ok := request.(*cluster.ConnectRequest); ok {
			reply, err = handleConnectRequest(v)
		}

	// DomainDescribeRequest
	case messages.DomainDescribeRequest:
		if v, ok := request.(*cluster.DomainDescribeRequest); ok {
			reply, err = handleDomainDescribeRequest(v)
		}

	// DomainRegisterRequest
	case messages.DomainRegisterRequest:
		if v, ok := request.(*cluster.DomainRegisterRequest); ok {
			reply, err = handleDomainRegisterRequest(v)
		}

	// DomainUpdateRequest
	case messages.DomainUpdateRequest:
		if v, ok := request.(*cluster.DomainUpdateRequest); ok {
			reply, err = handleDomainUpdateRequest(v)
		}

	// TerminateRequest
	case messages.TerminateRequest:
		if v, ok := request.(*cluster.TerminateRequest); ok {
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
		return http.StatusBadRequest, err
	}

	// Get the pointer to the ProxyMessage
	replyProxyMessage := reply.GetProxyMessage()

	// serialize the reply message into a []byte
	// to send back over the network
	serializedMessage, err := replyProxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return http.StatusBadRequest, err
	}

	// send the reply as an http.Request back to the Neon.Cadence Library
	// via http.PUT
	resp, err := putReply(serializedMessage, replyAddress)
	if err != nil {
		return http.StatusInternalServerError, err
	}
	defer resp.Body.Close()

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Neon.Cadence Library Response",
		zap.Int("ProxyReply Type", int(replyProxyMessage.Type)),
		zap.String("Response Status", resp.Status),
		zap.String("Request URL", resp.Request.URL.String()),
	)

	return resp.StatusCode, nil
}

func handleActivityRequest(request *activity.ActivityRequest) (base.IProxyMessage, error) {
	err := fmt.Errorf("not implemented exception for message type ActivityRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityRequest", zap.Error(err))
	return nil, err

}

func handleCancelRequest(request *cluster.CancelRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("CancelRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*cluster.CancelReply); ok {
			buildCancelReply(v, cadenceerrors.NewCadenceError(
				"ConnectionError",
				cadenceerrors.Custom,
				"no connection to the cadence server has been established yet"))
		}

		return reply, nil
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := cadenceclient.ClientHelper.Builder.BuildCadenceClient()
	if err != nil {
		if v, ok := reply.(*cluster.CancelReply); ok {
			buildCancelReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"failed to create cadence client"))
		}

		return reply, nil
	}

	targetRequestID := request.GetTargetRequestID()
	err = client.CancelWorkflow(context.Background(), string(targetRequestID), "")
	if err != nil {
		if v, ok := reply.(*cluster.CancelReply); ok {
			buildCancelReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				fmt.Sprintf("failed to cancel workflow with ID %d", targetRequestID)), false)
		}

		return reply, nil
	}

	if v, ok := reply.(*cluster.CancelReply); ok {
		buildCancelReply(v, nil, true)
	}

	return reply, nil
}

func handleConnectRequest(request *cluster.ConnectRequest) (base.IProxyMessage, error) {

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
		if v, ok := reply.(*cluster.ConnectReply); ok {
			buildConnectReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"failed to configure the cadence service client"))
		}
	}

	if v, ok := reply.(*cluster.ConnectReply); ok {
		buildConnectReply(v, nil)
	}

	return reply, nil
}

func handleDomainDescribeRequest(request *cluster.DomainDescribeRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainDescribeRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*cluster.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				"ConnectionError",
				cadenceerrors.Custom,
				"no connection to the cadence server has been established yet"))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*cluster.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"failed to create cadence domain client"))
		}

		return reply, nil
	}

	// send a describe domain request to the cadence server
	describeDomainResponse, err := domainClient.Describe(context.Background(), *request.GetName())
	if err != nil {
		if v, ok := reply.(*cluster.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"describe domain operation failed"))
		}

		return reply, nil
	}

	if v, ok := reply.(*cluster.DomainDescribeReply); ok {
		buildDomainDescribeReply(v, nil, describeDomainResponse)
	}

	return reply, nil
}

func handleDomainRegisterRequest(request *cluster.DomainRegisterRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*cluster.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				"ConnectionError",
				cadenceerrors.Custom,
				"no connection to the cadence server has been established yet"))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*cluster.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"failed to create cadence domain client"))
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
		if v, ok := reply.(*cluster.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				fmt.Sprintf("error while trying to register domain %s with cadence server", domainRegisterRequest.GetName())))
		}

		return reply, nil
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully registered", zap.String("Domain Name", domainRegisterRequest.GetName()))

	if v, ok := reply.(*cluster.DomainRegisterReply); ok {
		buildDomainRegisterReply(v, nil)
	}

	return reply, nil
}

func handleDomainUpdateRequest(request *cluster.DomainUpdateRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainUpdateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// create the reply to Neon.Cadence library
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if cadenceclient.ClientHelper == nil {
		if v, ok := reply.(*cluster.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				"ConnectionError",
				cadenceerrors.Custom,
				"no connection to the cadence server has been established yet"))
		}

		return reply, nil
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := cadenceclient.ClientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		if v, ok := reply.(*cluster.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"failed to create cadence domain client"))
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

		if v, ok := reply.(*cluster.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceerrors.NewCadenceError(
				err.Error(),
				cadenceerrors.Custom,
				"update domain operation failed"))
		}

		return reply, nil
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully Updated", zap.String("Domain Name", domainUpdateRequest.GetName()))

	if v, ok := reply.(*cluster.DomainUpdateReply); ok {
		buildDomainUpdateReply(v, nil)
	}

	return reply, nil
}

func handleHeartbeatRequest(request *cluster.HeartbeatRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("HeartbeatRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new HeartbeatReply
	reply := createReplyMessage(request)
	if v, ok := reply.(*cluster.HeartbeatReply); ok {
		buildHeartbeatReply(v, nil)
	}

	return reply, nil
}

func handleInitializeRequest(request *cluster.InitializeRequest) (base.IProxyMessage, error) {

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

	if v, ok := reply.(*cluster.InitializeReply); ok {
		buildInitializeReply(v, nil)
	}

	return reply, nil
}

func handleTerminateRequest(request *cluster.TerminateRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("TerminateRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true

	if v, ok := reply.(*cluster.TerminateReply); ok {
		buildTerminateReply(v, nil)
	}

	return reply, nil
}

func handleWorkflowRequest(request *workflow.WorkflowRequest) (base.IProxyMessage, error) {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowRequest Recieved", zap.Int("ProccessId", os.Getpid()))

	err := fmt.Errorf("not implemented exception for message type WorkflowRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowRequest", zap.Error(err))
	return nil, err

}

// -------------------------------------------------------------------------
// ProxyReply builders

func buildCancelReply(reply *cluster.CancelReply, cadenceError *cadenceerrors.CadenceError, wasCancelled ...bool) {
	reply.SetError(cadenceError)

	if len(wasCancelled) > 0 {
		reply.SetWasCancelled(wasCancelled[0])
	}
}

func buildConnectReply(reply *cluster.ConnectReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainDescribeReply(reply *cluster.DomainDescribeReply, cadenceError *cadenceerrors.CadenceError, describeDomainResponse ...*cadenceshared.DescribeDomainResponse) {
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

func buildDomainRegisterReply(reply *cluster.DomainRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainUpdateReply(reply *cluster.DomainUpdateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildHeartbeatReply(reply *cluster.HeartbeatReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildInitializeReply(reply *cluster.InitializeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildTerminateReply(reply *cluster.TerminateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply base.IProxyReply, typeCode messages.MessageType) (int, error) {

	// error to catch any exceptions thrown in the
	// switch block
	var err error

	// handle the messages individually based on their message type
	switch typeCode {

	// InitializeReply
	case messages.InitializeReply:
		err = handleInitializeReply(reply)

	// HeartbeatReply
	case messages.HeartbeatReply:
		err = handleHeartbeatReply(reply)

	// CancelReply
	case messages.CancelReply:
		err = handleCancelReply(reply)

	// ConnectReply
	case messages.ConnectReply:
		err = handleConnectReply(reply)

	// DomainDescribeReply
	case messages.DomainDescribeReply:
		err = handleDomainDescribeReply(reply)

	// DomainRegisterReply
	case messages.DomainRegisterReply:
		err = handleDomainRegisterReply(reply)

	// DomainUpdateReply
	case messages.DomainUpdateReply:
		err = handleDomainUpdateReply(reply)

	// TerminateReply
	case messages.TerminateReply:
		err = handleTerminateReply(reply)

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any exceptions returned in
	// the switch block
	if err != nil {
		return http.StatusBadRequest, err
	}

	return http.StatusOK, nil
}

func handleActivityReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type ActivityReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityReply", zap.Error(err))
	return err
}

func handleCancelReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type CancelReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling CancelReply", zap.Error(err))
	return err
}

func handleConnectReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type ConnectReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ConnectReply", zap.Error(err))
	return err
}

func handleDomainDescribeReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainDescribeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainDescribeReply", zap.Error(err))
	return err
}

func handleDomainRegisterReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainRegisterReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainRegisterReply", zap.Error(err))
	return err
}

func handleDomainUpdateReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainUpdateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainUpdateReply", zap.Error(err))
	return err
}

func handleHeartbeatReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type HeartbeatReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling HeartbeatReply", zap.Error(err))
	return err
}

func handleInitializeReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type InitializeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling InitializeReply", zap.Error(err))
	return err
}

func handleTerminateReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type TerminateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling TerminateReply", zap.Error(err))
	return err
}

func handleWorkflowReply(reply base.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowReply", zap.Error(err))
	return err
}
