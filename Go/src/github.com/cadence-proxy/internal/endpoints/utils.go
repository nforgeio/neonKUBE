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

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/cadence-proxy/internal"
	proxyclient "github.com/cadence-proxy/internal/cadence/client"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
	proxyworkflow "github.com/cadence-proxy/internal/cadence/workflow"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

//----------------------------------------------------------------------------
// ProxyMessage processing helpers

// CheckRequestValidity checks to make sure that the request is in
// the correct format to be handled
func CheckRequestValidity(w http.ResponseWriter, r *http.Request) (int, error) {
	Logger.Debug("Request Received",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProcessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != internal.ContentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			internal.ContentType,
		)

		Logger.Error("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", internal.ContentType),
			zap.Error(err),
		)

		return http.StatusBadRequest, err
	}

	if r.Method != http.MethodPut {
		err := fmt.Errorf("invalid HTTP Method: %s, must be HTTP Metho: %s",
			r.Method,
			http.MethodPut,
		)

		Logger.Error("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
			zap.Error(err),
		)

		return http.StatusMethodNotAllowed, err
	}

	return http.StatusOK, nil
}

// ReadAndDeserialize reads the ProxyMessage from the request body and
// deserializes it into the corresponding message type.
func ReadAndDeserialize(body io.Reader) (messages.IProxyMessage, error) {
	payload, err := ioutil.ReadAll(body)
	if err != nil {
		Logger.Error("Null request body", zap.Error(err))
		return nil, err
	}

	// deserialize the payload
	buf := bytes.NewBuffer(payload)
	message, err := messages.Deserialize(buf, false)
	if err != nil {
		Logger.Error("Error deserializing input", zap.Error(err))
		return nil, err
	}

	return message, nil
}

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {
	proxyMessage := message.GetProxyMessage()
	// Logger.Debug("Sending message to .net client",
	// 	zap.String("Address", replyAddress),
	// 	zap.String("MessageType", proxyMessage.Type.String()),
	// 	zap.Int("ProcessId", os.Getpid()),
	// )

	// serialize the message
	content, err := proxyMessage.Serialize(false)
	if err != nil {
		return nil, err
	}

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, replyAddress, buf)
	if err != nil {
		return nil, err
	}

	// set the request header to specified content type
	// and disable http request compression
	req.Header.Set("Content-Type", internal.ContentType)
	req.Header.Set("Accept-Encoding", "identity")

	// initialize the http.Client and send the request
	resp, err := httpClient.Do(req)
	if err != nil {
		return nil, err
	}

	return resp, nil
}

func NextRequestID() int64 {
	return messages.NextRequestID()
}

func setReplayStatus(ctx workflow.Context, message messages.IProxyMessage) {
	isReplaying := workflow.IsReplaying(ctx)
	switch s := message.(type) {
	case messages.IWorkflowReply:
		if isReplaying {
			s.SetReplayStatus(proxyworkflow.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(proxyworkflow.ReplayStatusNotReplaying)
		}
	case *messages.WorkflowInvokeRequest:
		if isReplaying {
			s.SetReplayStatus(proxyworkflow.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(proxyworkflow.ReplayStatusNotReplaying)
		}
	case *messages.WorkflowQueryInvokeRequest:
		if isReplaying {
			s.SetReplayStatus(proxyworkflow.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(proxyworkflow.ReplayStatusNotReplaying)
		}
	case *messages.WorkflowSignalInvokeRequest:
		if isReplaying {
			s.SetReplayStatus(proxyworkflow.ReplayStatusReplaying)
		} else {
			s.SetReplayStatus(proxyworkflow.ReplayStatusNotReplaying)
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
			panic(err)
		}
	}()
}

func sendFutureACK(contextID, operationID, clientID int64) *messages.Operation {

	// create the WorkflowFutureReadyRequest
	requestID := NextRequestID()
	workflowFutureReadyRequest := messages.NewWorkflowFutureReadyRequest()
	workflowFutureReadyRequest.SetRequestID(requestID)
	workflowFutureReadyRequest.SetContextID(contextID)
	workflowFutureReadyRequest.SetFutureOperationID(operationID)
	workflowFutureReadyRequest.SetClientID(clientID)

	// create the Operation for this request and add it to the operations map
	op := messages.NewOperation(requestID, workflowFutureReadyRequest)
	op.SetChannel(make(chan interface{}))
	op.SetContextID(contextID)
	Operations.Add(requestID, op)

	// send the request
	go sendMessage(workflowFutureReadyRequest)

	return op
}

func isCanceledErr(err interface{}) bool {
	var errStr string
	if v, ok := err.(*proxyerror.CadenceError); ok {
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

func verifyClientHelper(request messages.IProxyRequest, helper *proxyclient.ClientHelper) error {
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
			return internal.ErrConnection
		}
	}

	return nil
}

func workflowRegisterWithOptions(workflowFunc interface{}, opts workflow.RegisterOptions) {
	defer func() {
		if r := recover(); r != nil {
			if v, ok := r.(error); ok {
				if strings.Contains(v.Error(), "already registered") {
					return
				}
			}
			panic(r)
		}
	}()
	workflow.RegisterWithOptions(workflowFunc, opts)
}

func activityRegisterWithOptions(activityFunc interface{}, opts activity.RegisterOptions) {
	defer func() {
		if r := recover(); r != nil {
			if v, ok := r.(error); ok {
				if strings.Contains(v.Error(), "already registered") {
					return
				}
			}
			panic(r)
		}
	}()
	activity.RegisterWithOptions(activityFunc, opts)
}
