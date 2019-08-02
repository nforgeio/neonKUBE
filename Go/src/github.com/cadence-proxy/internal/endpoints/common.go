//-----------------------------------------------------------------------------
// FILE:		common.go
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
	"fmt"
	"io"
	"io/ioutil"
	"net/http"
	"os"
	"strings"
	"time"

	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	globals "github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/cadence/cadenceactivities"
	"github.com/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
	"github.com/cadence-proxy/internal/server"
)

var (

	// logger for all endpoints to utilize
	logger *zap.Logger

	// Instance is a pointer to the server instance of the current server that the
	// cadence-proxy is listening on.  This gets set in main.go
	Instance *server.Instance

	// replyAddress specifies the address that the Neon.Cadence library
	// will be listening on for replies from the cadence proxy
	replyAddress string

	// terminate is a boolean that will be set after handling an incoming
	// TerminateRequest.  A true value will indicate that the server instance
	// needs to gracefully shut down after handling the request, and a false value
	// indicates the server continues to run
	terminate bool

	// cadenceClientTimeout specifies the amount of time in seconds a reply has to be sent after
	// a request has been received by the cadence-proxy
	cadenceClientTimeout time.Duration = time.Minute

	// ClientHelper is a global variable that holds this cadence-proxy's instance
	// of the ClientHelper that will be used to create domain and workflow clients
	// that communicate with the cadence server
	clientHelper = cadenceclient.NewClientHelper()

	// ActivityContexts maps a int64 ContextId to the cadence
	// Activity Context passed to the cadence Activity functions.
	// The cadence-client will use contextIds to refer to specific
	// activity contexts when perfoming activity actions
	ActivityContexts = new(cadenceactivities.ActivityContextsMap)

	// Workers maps a int64 WorkerId to the cadence
	// Worker returned by the Cadence NewWorker() function.
	// This will be used to stop a worker via the
	// StopWorkerRequest.
	Workers = new(cadenceworkers.WorkersMap)

	// WorkflowContexts maps a int64 ContextId to the cadence
	// Workflow Context passed to the cadence Workflow functions.
	// The cadence-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowContexts = new(cadenceworkflows.WorkflowContextsMap)

	// Operations is a map of operations used to track pending
	// cadence-client operations
	Operations = new(operationsMap)

	// httpClient is the HTTP client used to send requests
	// to the Neon.Cadence client
	httpClient = http.Client{}
)

//----------------------------------------------------------------------------
// ProxyMessage processing helpers

func checkRequestValidity(w http.ResponseWriter, r *http.Request) (int, error) {

	// log when a new request has come in
	logger.Info("Request Received",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProccessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != globals.ContentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			globals.ContentType,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", globals.ContentType),
			zap.Error(err),
		)

		return http.StatusBadRequest, err
	}

	if r.Method != http.MethodPut {
		err := fmt.Errorf("invalid HTTP Method: %s, must be HTTP Metho: %s",
			r.Method,
			http.MethodPut,
		)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
			zap.Error(err),
		)

		return http.StatusMethodNotAllowed, err
	}

	return http.StatusOK, nil
}

func readAndDeserialize(body io.Reader) (messages.IProxyMessage, error) {

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(body)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Null request body", zap.Error(err))
		return nil, err
	}

	// deserialize the payload
	buf := bytes.NewBuffer(payload)
	message, err := messages.Deserialize(buf, false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Error deserializing input", zap.Error(err))
		return nil, err
	}

	return message, nil
}

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {

	// $debug(jack.burns): DELETE THIS!
	proxyMessage := message.GetProxyMessage()
	logger.Info("Sending request to Neon.Cadence client",
		zap.String("Address", replyAddress),
		zap.String("MessageType", proxyMessage.Type.String()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// serialize the message
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
	req.Header.Set("Content-Type", globals.ContentType)
	req.Header.Set("Accept-Encoding", "identity")

	// initialize the http.Client and send the request
	resp, err := httpClient.Do(req)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Error("Error sending Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	return resp, nil
}

func verifyClientHelper(request messages.IProxyRequest, helper *cadenceclient.ClientHelper) error {
	switch request.GetType() {
	case messagetypes.InitializeRequest,
		messagetypes.PingRequest,
		messagetypes.ConnectRequest,
		messagetypes.TerminateRequest,
		messagetypes.CancelRequest,
		messagetypes.HeartbeatRequest:
		return nil
	default:
		if helper == nil {
			return globals.ErrConnection
		}
	}

	return nil
}

func setReplayStatus(ctx workflow.Context, message messages.IProxyMessage) {
	isReplaying := workflow.IsReplaying(ctx)
	switch s := message.(type) {
	case messages.IWorkflowReply:
		if isReplaying {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusNotReplaying)
		}
	case *messages.WorkflowInvokeRequest:
		if isReplaying {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusNotReplaying)
		}
	case *messages.WorkflowQueryInvokeRequest:
		if isReplaying {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusNotReplaying)
		}
	case *messages.WorkflowSignalInvokeRequest:
		if isReplaying {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(cadenceworkflows.ReplayStatusNotReplaying)
		}
	}
}

func sendMessage(message messages.IProxyMessage) {
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

func isCanceledErr(err interface{}) bool {
	var errStr string
	if v, ok := err.(*cadenceerrors.CadenceError); ok {
		errStr = v.ToString()
	}

	if v, ok := err.(error); ok {
		errStr = v.Error()
	}

	return strings.Contains(errStr, "CanceledError")
}
