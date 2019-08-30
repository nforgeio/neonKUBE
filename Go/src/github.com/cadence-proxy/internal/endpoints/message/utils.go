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

package message

import (
	"bytes"
	"net/http"
	"os"
	"strings"

	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/cadence-proxy/internal"
	proxyclient "github.com/cadence-proxy/internal/cadence/client"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
	proxyworkflow "github.com/cadence-proxy/internal/cadence/workflow"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {
	proxyMessage := message.GetProxyMessage()
	internal.Logger.Debug("Sending message to .net client",
		zap.String("Address", replyAddress),
		zap.String("MessageType", proxyMessage.Type.String()),
		zap.Int("ProcessId", os.Getpid()),
	)

	// serialize the message
	content, err := proxyMessage.Serialize(false)
	if err != nil {
		internal.Logger.Error("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, replyAddress, buf)
	if err != nil {
		internal.Logger.Error("Error creating .net client request", zap.Error(err))
		return nil, err
	}

	// set the request header to specified content type
	// and disable http request compression
	req.Header.Set("Content-Type", internal.ContentType)
	req.Header.Set("Accept-Encoding", "identity")

	// initialize the http.Client and send the request
	resp, err := httpClient.Do(req)
	if err != nil {
		internal.Logger.Error("Error sending .net client request", zap.Error(err))
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
			internal.Logger.Error("could not close response body", zap.Error(err))
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
