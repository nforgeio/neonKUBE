package endpoints

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/cluster"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

	"go.uber.org/zap"
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
)

// MessageHandler accepts an http.PUT requests and parses the
// request body, converts it into an ProxyMessage object
// and talks through the uber cadence client to the cadence server,
// executing the instructions incoded in the request.
//
// param w http.ResponseWriter
// param r *http.Request
func MessageHandler(w http.ResponseWriter, r *http.Request) {

	// grab the zap global logger
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
	// shut down the server instance
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
	message, err := base.Deserialize(buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error deserializing input", zap.Error(err))
		return http.StatusBadRequest, err
	}

	// determine whether the input request is a ProxyReply or ProxyRequest
	switch messageType := message.(type) {

	// Nil type value
	case nil:
		err := fmt.Errorf("nil type for incoming ProxyMessage: %v of type %v", message, messageType)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error processing incoming message", zap.Error(err))
		return http.StatusBadRequest, err

	// IProxyRequest
	case base.IProxyRequest:
		return handleIProxyRequest(message)

	// IProxyReply
	case base.IProxyReply:
		return handleIProxyReply(message)

	// Unrecognized type
	default:
		err := fmt.Errorf("unhandled message type. could not complete type assertion for type %v", messageType)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error processing incoming message", zap.Error(err))
		return http.StatusBadRequest, err
	}
}

func createReplyMessage(request base.IProxyMessage) (base.IProxyMessage, error) {

	// get the correct reply type and initialize a new
	// reply corresponding to the request message type
	if v, ok := request.(base.IProxyRequest); ok {
		key := int(v.GetReplyType())
		proxyMessage := base.MessageTypeStructMap[key].Clone()
		proxyMessage.SetRequestID(request.GetRequestID())
		return proxyMessage, nil
	}

	err := fmt.Errorf("could not create message reply of type %d", request.GetProxyMessage().Type)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error creating message reply", zap.Error(err))
	return nil, err
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
	req.Header.Set("Content-Type", _contentType)

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

func handleIProxyRequest(request base.IProxyMessage) (int, error) {

	// get the ProxyMessage from the IProxyMessage to check the
	// message type
	requestProxyMessage := request.GetProxyMessage()

	// error for catching exceptions in the switch block
	var err error

	// handle the messages individually based on their message type
	switch requestProxyMessage.Type {

	// InitializeRequest
	case messages.InitializeRequest:
		err = handleInitializeRequest(request)

	// HeartbeatRequest
	case messages.HeartbeatRequest:
		err = handleHeartbeatRequest(request)

	// CancelRequest
	case messages.CancelRequest:
		err = handleCancelRequest(request)

	// ConnectRequest
	case messages.ConnectRequest:
		err = handleConnectRequest(request)

	// DomainDescribeRequest
	case messages.DomainDescribeRequest:
		err = handleDomainDescribeRequest(request)

	// DomainRegisterRequest
	case messages.DomainRegisterRequest:
		err = handleDomainRegisterRequest(request)

	// DomainUpdateRequest
	case messages.DomainUpdateRequest:
		err = handleDomainUpdateRequest(request)

	// TerminateRequest
	case messages.TerminateRequest:
		err = handleTerminateRequest(request)

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", requestProxyMessage.Type)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any errors that may have occurred
	// in the switch block
	if err != nil {
		return http.StatusBadRequest, err
	}

	// create the reply to the incoming request
	reply, err := createReplyMessage(request)
	if err != nil {
		return http.StatusBadRequest, err
	}

	// Get the pointer to the ProxyMessage
	replyProxyMessage := reply.GetProxyMessage()

	// serialize the reply message into a []byte
	// to send back over the network
	serializedMessage, err := replyProxyMessage.Serialize()
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
		zap.String("Response Status", resp.Status),
		zap.String("Request URL", resp.Request.URL.String()),
	)

	return resp.StatusCode, nil
}

func handleActivityRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type ActivityRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityRequest", zap.Error(err))
	return err

}

func handleCancelRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type CancelRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling CancelRequest", zap.Error(err))
	return err

}

func handleConnectRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type ConnectRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ConnectRequest", zap.Error(err))
	return err

}

func handleDomainDescribeRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type DomainDescribeRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainDescribeRequest", zap.Error(err))
	return err

}

func handleDomainRegisterRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type DomainRegisterRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainRegisterRequest", zap.Error(err))
	return err

}

func handleDomainUpdateRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type DomainUpdateRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainUpdateRequest", zap.Error(err))
	return err

}

func handleHeartbeatRequest(request base.IProxyMessage) error {
	if _, ok := request.(*cluster.HeartbeatRequest); ok {
		return nil
	}

	err := fmt.Errorf("unhandled exception during type assertion of HeartbeatRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling HeartbeatRequest", zap.Error(err))
	return err

}

func handleInitializeRequest(request base.IProxyMessage) error {
	if initializeRequest, ok := request.(*cluster.InitializeRequest); ok {
		address := *initializeRequest.GetLibraryAddress()
		port := *initializeRequest.GetLibraryPort()
		replyAddress = fmt.Sprintf("http://%s:%s/",
			address,
			port,
		)

		// $debug(jack.burns): DELETE THIS!
		//replyAddress = "http://127.0.0.2:5001/"

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("InitializeRequest info",
			zap.String("Library Address", address),
			zap.String("LibaryPort", port),
			zap.String("Reply Address", replyAddress),
		)

		return nil
	}

	err := fmt.Errorf("unhandled exception during type assertion of InitializeRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling InitializeRequest", zap.Error(err))
	return err

}

func handleTerminateRequest(request base.IProxyMessage) error {

	if _, ok := request.(*cluster.TerminateRequest); ok {

		// setting terminate to true indicates that after the TerminateReply is sent,
		// the server instance should gracefully shut down
		terminate = true
		return nil
	}

	err := fmt.Errorf("not implemented exception for message type TerminateRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling TerminateRequest", zap.Error(err))
	return err
}

func handleWorkflowRequest(request base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowRequest")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowRequest", zap.Error(err))
	return err

}

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply base.IProxyMessage) (int, error) {

	// get the ProxyMessage from the IProxyMessage to check the
	// message type
	replyProxyMessage := reply.GetProxyMessage()

	// error to catch any exceptions thrown in the
	// switch block
	var err error

	// handle the messages individually based on their message type
	switch replyProxyMessage.Type {

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

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", replyProxyMessage.Type)

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

func handleActivityReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type ActivityReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityReply", zap.Error(err))
	return err
}

func handleCancelReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type CancelReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling CancelReply", zap.Error(err))
	return err
}

func handleConnectReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type ConnectReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ConnectReply", zap.Error(err))
	return err
}

func handleDomainDescribeReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type DomainDescribeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainDescribeReply", zap.Error(err))
	return err
}

func handleDomainRegisterReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type DomainRegisterReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainRegisterReply", zap.Error(err))
	return err
}

func handleDomainUpdateReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type DomainUpdateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainUpdateReply", zap.Error(err))
	return err
}

func handleHeartbeatReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type HeartbeatReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling HeartbeatReply", zap.Error(err))
	return err
}

func handleInitializeReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type InitializeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling InitializeReply", zap.Error(err))
	return err
}

func handleTerminateReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type TerminateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling TerminateReply", zap.Error(err))
	return err
}

func handleWorkflowReply(reply base.IProxyMessage) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowReply", zap.Error(err))
	return err
}

// ConfigureCadenceClientHelper takes an ProxyMessage and
// creates a new cadence Helper instance.  The Helper is used to make
// calls to the cadence client and holds all of the configurations
// for actually building a cadence client instance, building a
// cadence domain client, and configuring the TChannels on which calls
// to the cadence server from the client will be made
//
// Param args map[string]*string -> the ProxyMessage Properties
// holding the configuration data to used to initialize the
// cadence Helper and construct the cadence client
// and the cadence domain client
//
// Returns *cadence.Helper -> a pointer to a new cadence.Helper which can be used
// to make cadence client calls to the cadence server
// Returns err -> any errors that might be thrown during the initialization of cadence
// Helper
func ConfigureCadenceClientHelper(args map[string]*string) (*cadenceclient.CadenceClientHelper, error) {

	var h *cadenceclient.CadenceClientHelper
	var err error

	return h, err

}
