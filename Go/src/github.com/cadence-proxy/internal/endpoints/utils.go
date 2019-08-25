//-----------------------------------------------------------------------------
// FILE:		utils.go
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

	globals "github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"
)

//----------------------------------------------------------------------------
// ProxyMessage processing helpers

func checkRequestValidity(w http.ResponseWriter, r *http.Request) (int, error) {
	logger.Debug("Request Received",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProcessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != globals.ContentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			globals.ContentType,
		)

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

		logger.Error("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
			zap.Error(err),
		)

		return http.StatusMethodNotAllowed, err
	}

	return http.StatusOK, nil
}

func readAndDeserialize(body io.Reader) (messages.IProxyMessage, error) {
	payload, err := ioutil.ReadAll(body)
	if err != nil {
		logger.Error("Null request body", zap.Error(err))
		return nil, err
	}

	// deserialize the payload
	buf := bytes.NewBuffer(payload)
	message, err := messages.Deserialize(buf, false)
	if err != nil {
		logger.Error("Error deserializing input", zap.Error(err))
		return nil, err
	}

	return message, nil
}

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {
	proxyMessage := message.GetProxyMessage()
	logger.Debug("Sending message to .net client",
		zap.String("Address", replyAddress),
		zap.String("MessageType", proxyMessage.Type.String()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// serialize the message
	content, err := proxyMessage.Serialize(false)
	if err != nil {
		logger.Error("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, replyAddress, buf)
	if err != nil {
		logger.Error("Error creating .net client request", zap.Error(err))
		return nil, err
	}

	// set the request header to specified content type
	// and disable http request compression
	req.Header.Set("Content-Type", globals.ContentType)
	req.Header.Set("Accept-Encoding", "identity")

	// initialize the http.Client and send the request
	resp, err := httpClient.Do(req)
	if err != nil {
		logger.Error("Error sending .net client request", zap.Error(err))
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
		err := resp.Body.Close()
		if err != nil {
			logger.Error("could not close response body", zap.Error(err))
		}
	}()
}

func sendFutureACK(contextID, operationID, clientID int64) *Operation {

	// create the WorkflowFutureReadyRequest
	requestID := NextRequestID()
	workflowFutureReadyRequest := messages.NewWorkflowFutureReadyRequest()
	workflowFutureReadyRequest.SetRequestID(requestID)
	workflowFutureReadyRequest.SetContextID(contextID)
	workflowFutureReadyRequest.SetFutureOperationID(operationID)
	workflowFutureReadyRequest.SetClientID(clientID)

	// create the Operation for this request and add it to the operations map
	op := NewOperation(requestID, workflowFutureReadyRequest)
	op.SetChannel(make(chan interface{}))
	op.SetContextID(contextID)
	Operations.Add(requestID, op)

	// send the request
	go sendMessage(workflowFutureReadyRequest)

	return op
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

func isForceReplayErr(err error) bool {
	return strings.Contains(err.Error(), "force-replay")
}
