//-----------------------------------------------------------------------------
// FILE:		message.go
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
	"context"
	"fmt"
	"net/http"
	"runtime/debug"
	"sync"

	"go.uber.org/zap"

	"github.com/cadence-proxy/internal"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

var (

	// Mutex lock to keep race conditions from happening
	// when terminating the cadence-proxy
	terminateMu sync.Mutex
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
	// check if the request has the correct content type
	// and is an http.PUT request
	statusCode, err := CheckRequestValidity(w, r)
	if err != nil {
		http.Error(w, err.Error(), statusCode)
		return
	}

	// read and deserialize the body
	message, err := ReadAndDeserialize(r.Body)
	if err != nil {
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	// make channel for writing a response to the sender
	// go routine to process the message
	// receive error on responseChan
	responseChan := make(chan error)
	go processIncomingMessage(message, responseChan)

	// block and wait for error value
	// on responseChan to send an http.Response
	// back to the Neon.Cadence Lib
	err = <-responseChan
	if err != nil {
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	w.WriteHeader(http.StatusOK)
}

// -------------------------------------------------------------------------
// Helper methods for handling incoming messages

func processIncomingMessage(message messages.IProxyMessage, responseChan chan error) {

	// defer the termination of the server
	// and the closing of the responseChan
	defer func() {

		// close the responseChan
		close(responseChan)

		// check to see if terminate is true, if it is then gracefully
		// shut down the server instance by sending a truth bool value
		// to the instance's ShutdownChannel
		terminateMu.Lock()
		if terminate {
			Instance.ShutdownChannel <- true
			terminate = false
		}
		terminateMu.Unlock()
	}()

	// switch on message interface type (IProxyRequest/IProxyReply)
	var err error
	switch s := message.(type) {
	case nil:
		err = fmt.Errorf("nil type for incoming ProxyMessage of type %v", message.GetType())
		Logger.Debug("Error processing incoming message", zap.Error(err))
		responseChan <- err

	// IProxyRequest
	case messages.IProxyRequest:
		responseChan <- nil
		err = handleIProxyRequest(s)

	// IProxyReply
	case messages.IProxyReply:
		responseChan <- nil
		err = handleIProxyReply(s)

	// Unrecognized type
	default:
		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %v", message.GetType())
		Logger.Debug("Error processing incoming message", zap.Error(err))
		responseChan <- err
	}

	// there should not be error values in here
	// if there are, then something is wrong with the server
	// and most likely needs to be terminated
	if err != nil {
		Logger.Error("Error Handling ProxyMessage", zap.Error(err))
	}
}

// -------------------------------------------------------------------------
// IProxyRequest message type handler entrypoint

func handleIProxyRequest(request messages.IProxyRequest) (err error) {
	var reply messages.IProxyReply

	// defer panic recovery
	defer func() {
		if r := recover(); r != nil {
			reply = createReplyMessage(request)
			err = fmt.Errorf("Panic: %v\nMessageType: %s, RequestId: %d.\n%s",
				r,
				request.GetType().String(),
				request.GetRequestID(),
				string(debug.Stack()),
			)
			buildReply(reply, proxyerror.NewCadenceError(err))
			Logger.Error("Panic", zap.Error(err))

			// send the reply
			var resp *http.Response
			resp, err = putToNeonCadenceClient(reply)
			if err != nil {
				Logger.Fatal(err.Error())
			}
			err = resp.Body.Close()
			if err != nil {
				return
			}
		}
	}()

	// check for a cadence server connection.
	// if it exists, then enter switch block to handle the
	// specified request type
	if err = verifyClientHelper(request, Clients.Get(request.GetClientID())); err != nil {
		reply = createReplyMessage(request)
		buildReply(reply, proxyerror.NewCadenceError(err))
	} else {

		// create a context for every request
		// handle the messages individually
		// based on their message type
		ctx := context.Background()
		switch request.GetType() {

		// -------------------------------------------------------------------------
		// Client message types

		// InitializeRequest
		case messagetypes.InitializeRequest:
			if v, ok := request.(*messages.InitializeRequest); ok {
				reply = handleInitializeRequest(ctx, v)
			}

		// HeartbeatRequest
		case messagetypes.HeartbeatRequest:
			if v, ok := request.(*messages.HeartbeatRequest); ok {
				reply = handleHeartbeatRequest(ctx, v)
			}

		// CancelRequest
		case messagetypes.CancelRequest:
			if v, ok := request.(*messages.CancelRequest); ok {
				reply = handleCancelRequest(ctx, v)
			}

		// ConnectRequest
		case messagetypes.ConnectRequest:
			if v, ok := request.(*messages.ConnectRequest); ok {
				reply = handleConnectRequest(ctx, v)
			}

		// DisconnectRequest
		case messagetypes.DisconnectRequest:
			if v, ok := request.(*messages.DisconnectRequest); ok {
				reply = handleDisconnectRequest(ctx, v)
			}

		// DomainDescribeRequest
		case messagetypes.DomainDescribeRequest:
			if v, ok := request.(*messages.DomainDescribeRequest); ok {
				reply = handleDomainDescribeRequest(ctx, v)
			}

		// DomainRegisterRequest
		case messagetypes.DomainRegisterRequest:
			if v, ok := request.(*messages.DomainRegisterRequest); ok {
				reply = handleDomainRegisterRequest(ctx, v)
			}

		// DomainUpdateRequest
		case messagetypes.DomainUpdateRequest:
			if v, ok := request.(*messages.DomainUpdateRequest); ok {
				reply = handleDomainUpdateRequest(ctx, v)
			}

		// TerminateRequest
		case messagetypes.TerminateRequest:
			if v, ok := request.(*messages.TerminateRequest); ok {
				reply = handleTerminateRequest(ctx, v)
			}

		// NewWorkerRequest
		case messagetypes.NewWorkerRequest:
			if v, ok := request.(*messages.NewWorkerRequest); ok {
				reply = handleNewWorkerRequest(ctx, v)
			}

		// StopWorkerRequest
		case messagetypes.StopWorkerRequest:
			if v, ok := request.(*messages.StopWorkerRequest); ok {
				reply = handleStopWorkerRequest(ctx, v)
			}

		// PingRequest
		case messagetypes.PingRequest:
			if v, ok := request.(*messages.PingRequest); ok {
				reply = handlePingRequest(ctx, v)
			}

		// -------------------------------------------------------------------------
		// Workflow message types

		// WorkflowRegisterRequest
		case messagetypes.WorkflowRegisterRequest:
			if v, ok := request.(*messages.WorkflowRegisterRequest); ok {
				reply = handleWorkflowRegisterRequest(ctx, v)
			}

		// WorkflowExecuteRequest
		case messagetypes.WorkflowExecuteRequest:
			if v, ok := request.(*messages.WorkflowExecuteRequest); ok {
				reply = handleWorkflowExecuteRequest(ctx, v)
			}

		// WorkflowCancelRequest
		case messagetypes.WorkflowCancelRequest:
			if v, ok := request.(*messages.WorkflowCancelRequest); ok {
				reply = handleWorkflowCancelRequest(ctx, v)
			}

		// WorkflowTerminateRequest
		case messagetypes.WorkflowTerminateRequest:
			if v, ok := request.(*messages.WorkflowTerminateRequest); ok {
				reply = handleWorkflowTerminateRequest(ctx, v)
			}

		// WorkflowSignalWithStartRequest
		case messagetypes.WorkflowSignalWithStartRequest:
			if v, ok := request.(*messages.WorkflowSignalWithStartRequest); ok {
				reply = handleWorkflowSignalWithStartRequest(ctx, v)
			}

		// WorkflowSetCacheSizeRequest
		case messagetypes.WorkflowSetCacheSizeRequest:
			if v, ok := request.(*messages.WorkflowSetCacheSizeRequest); ok {
				reply = handleWorkflowSetCacheSizeRequest(ctx, v)
			}

		// WorkflowQueryRequest
		case messagetypes.WorkflowQueryRequest:
			if v, ok := request.(*messages.WorkflowQueryRequest); ok {
				reply = handleWorkflowQueryRequest(ctx, v)
			}

		// WorkflowMutableRequest
		case messagetypes.WorkflowMutableRequest:
			if v, ok := request.(*messages.WorkflowMutableRequest); ok {
				reply = handleWorkflowMutableRequest(ctx, v)
			}

		// WorkflowDescribeExecutionRequest
		case messagetypes.WorkflowDescribeExecutionRequest:
			if v, ok := request.(*messages.WorkflowDescribeExecutionRequest); ok {
				reply = handleWorkflowDescribeExecutionRequest(ctx, v)
			}

		// WorkflowGetResultRequest
		case messagetypes.WorkflowGetResultRequest:
			if v, ok := request.(*messages.WorkflowGetResultRequest); ok {
				reply = handleWorkflowGetResultRequest(ctx, v)
			}

		// WorkflowSignalSubscribeRequest
		case messagetypes.WorkflowSignalSubscribeRequest:
			if v, ok := request.(*messages.WorkflowSignalSubscribeRequest); ok {
				reply = handleWorkflowSignalSubscribeRequest(ctx, v)
			}

		// WorkflowSignalRequest
		case messagetypes.WorkflowSignalRequest:
			if v, ok := request.(*messages.WorkflowSignalRequest); ok {
				reply = handleWorkflowSignalRequest(ctx, v)
			}

		// WorkflowHasLastResultRequest
		case messagetypes.WorkflowHasLastResultRequest:
			if v, ok := request.(*messages.WorkflowHasLastResultRequest); ok {
				reply = handleWorkflowHasLastResultRequest(ctx, v)
			}

		// WorkflowGetLastResultRequest
		case messagetypes.WorkflowGetLastResultRequest:
			if v, ok := request.(*messages.WorkflowGetLastResultRequest); ok {
				reply = handleWorkflowGetLastResultRequest(ctx, v)
			}

		// WorkflowDisconnectContextRequest
		case messagetypes.WorkflowDisconnectContextRequest:
			if v, ok := request.(*messages.WorkflowDisconnectContextRequest); ok {
				reply = handleWorkflowDisconnectContextRequest(ctx, v)
			}

		// WorkflowGetTimeRequest
		case messagetypes.WorkflowGetTimeRequest:
			if v, ok := request.(*messages.WorkflowGetTimeRequest); ok {
				reply = handleWorkflowGetTimeRequest(ctx, v)
			}

		// WorkflowSleepRequest
		case messagetypes.WorkflowSleepRequest:
			if v, ok := request.(*messages.WorkflowSleepRequest); ok {
				reply = handleWorkflowSleepRequest(ctx, v)
			}

		// WorkflowExecuteChildRequest
		case messagetypes.WorkflowExecuteChildRequest:
			if v, ok := request.(*messages.WorkflowExecuteChildRequest); ok {
				reply = handleWorkflowExecuteChildRequest(ctx, v)
			}

		// WorkflowWaitForChildRequest
		case messagetypes.WorkflowWaitForChildRequest:
			if v, ok := request.(*messages.WorkflowWaitForChildRequest); ok {
				reply = handleWorkflowWaitForChildRequest(ctx, v)
			}

		// WorkflowSignalChildRequest
		case messagetypes.WorkflowSignalChildRequest:
			if v, ok := request.(*messages.WorkflowSignalChildRequest); ok {
				reply = handleWorkflowSignalChildRequest(ctx, v)
			}

		// WorkflowCancelChildRequest
		case messagetypes.WorkflowCancelChildRequest:
			if v, ok := request.(*messages.WorkflowCancelChildRequest); ok {
				reply = handleWorkflowCancelChildRequest(ctx, v)
			}

		// WorkflowSetQueryHandlerRequest
		case messagetypes.WorkflowSetQueryHandlerRequest:
			if v, ok := request.(*messages.WorkflowSetQueryHandlerRequest); ok {
				reply = handleWorkflowSetQueryHandlerRequest(ctx, v)
			}

		// WorkflowGetVersionRequest
		case messagetypes.WorkflowGetVersionRequest:
			if v, ok := request.(*messages.WorkflowGetVersionRequest); ok {
				reply = handleWorkflowGetVersionRequest(ctx, v)
			}

		// -------------------------------------------------------------------------
		// Activity message types

		// ActivityExecuteRequest
		case messagetypes.ActivityExecuteRequest:
			if v, ok := request.(*messages.ActivityExecuteRequest); ok {
				reply = handleActivityExecuteRequest(ctx, v)
			}

		// ActivityRegisterRequest
		case messagetypes.ActivityRegisterRequest:
			if v, ok := request.(*messages.ActivityRegisterRequest); ok {
				reply = handleActivityRegisterRequest(ctx, v)
			}

		// ActivityHasHeartbeatDetailsRequest
		case messagetypes.ActivityHasHeartbeatDetailsRequest:
			if v, ok := request.(*messages.ActivityHasHeartbeatDetailsRequest); ok {
				reply = handleActivityHasHeartbeatDetailsRequest(ctx, v)
			}

		// ActivityGetHeartbeatDetailsRequest
		case messagetypes.ActivityGetHeartbeatDetailsRequest:
			if v, ok := request.(*messages.ActivityGetHeartbeatDetailsRequest); ok {
				reply = handleActivityGetHeartbeatDetailsRequest(ctx, v)
			}

		// ActivityRecordHeartbeatRequest
		case messagetypes.ActivityRecordHeartbeatRequest:
			if v, ok := request.(*messages.ActivityRecordHeartbeatRequest); ok {
				reply = handleActivityRecordHeartbeatRequest(ctx, v)
			}

		// ActivityGetInfoRequest
		case messagetypes.ActivityGetInfoRequest:
			if v, ok := request.(*messages.ActivityGetInfoRequest); ok {
				reply = handleActivityGetInfoRequest(ctx, v)
			}

		// ActivityCompleteRequest
		case messagetypes.ActivityCompleteRequest:
			if v, ok := request.(*messages.ActivityCompleteRequest); ok {
				reply = handleActivityCompleteRequest(ctx, v)
			}

		// ActivityExecuteLocalRequest
		case messagetypes.ActivityExecuteLocalRequest:
			if v, ok := request.(*messages.ActivityExecuteLocalRequest); ok {
				reply = handleActivityExecuteLocalRequest(ctx, v)
			}

		// Undefined message type
		default:
			e := fmt.Errorf("Unhandled message type. could not complete type assertion for type %d.", request.GetType())
			Logger.Error("Unhandled message type. Could not complete type assertion.", zap.Error(e))

			// set the reply
			reply = messages.NewProxyReply()
			reply.SetRequestID(request.GetRequestID())
			reply.SetError(proxyerror.NewCadenceError(e, proxyerror.Custom))
		}
	}

	// send the reply
	var resp *http.Response
	resp, err = putToNeonCadenceClient(reply)
	if err != nil {
		Logger.Fatal(err.Error())
	}
	err = resp.Body.Close()
	if err != nil {
		return
	}

	return
}

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply messages.IProxyReply) (err error) {
	defer func() {
		requestID := reply.GetRequestID()

		// recover from panic
		if r := recover(); r != nil {
			err = fmt.Errorf("Panic: %v. MessageType: %s, RequestId: %d. %s",
				r,
				reply.GetType().String(),
				reply.GetRequestID(),
				string(debug.Stack()),
			)
			Logger.Error("Panic", zap.Error(err))
		}

		// remove the operation
		Operations.Remove(requestID)
	}()

	// check to make sure that the operation exists
	op := Operations.Get(reply.GetRequestID())
	if op == nil {
		err = internal.ErrEntityNotExist
	} else {

		// handle the messages individually based on their message type
		switch reply.GetType() {

		// -------------------------------------------------------------------------
		// client message types

		// LogReply
		case messagetypes.LogReply:
			if v, ok := reply.(*messages.LogReply); ok {
				err = handleLogReply(v, op)
			}

		// -------------------------------------------------------------------------
		// Workflow message types

		// WorkflowInvokeReply
		case messagetypes.WorkflowInvokeReply:
			if v, ok := reply.(*messages.WorkflowInvokeReply); ok {
				err = handleWorkflowInvokeReply(v, op)
			}

		// WorkflowSignalInvokeReply
		case messagetypes.WorkflowSignalInvokeReply:
			if v, ok := reply.(*messages.WorkflowSignalInvokeReply); ok {
				err = handleWorkflowSignalInvokeReply(v, op)
			}

		// WorkflowQueryInvokeReply
		case messagetypes.WorkflowQueryInvokeReply:
			if v, ok := reply.(*messages.WorkflowQueryInvokeReply); ok {
				err = handleWorkflowQueryInvokeReply(v, op)
			}

		// WorkflowFutureReadyReply
		case messagetypes.WorkflowFutureReadyReply:
			if v, ok := reply.(*messages.WorkflowFutureReadyReply); ok {
				err = handleWorkflowFutureReadyReply(v, op)
			}

		// -------------------------------------------------------------------------
		// Activity message types

		// ActivityInvokeReply
		case messagetypes.ActivityInvokeReply:
			if v, ok := reply.(*messages.ActivityInvokeReply); ok {
				err = handleActivityInvokeReply(v, op)
			}

		// ActivityStoppingReply
		case messagetypes.ActivityStoppingReply:
			if v, ok := reply.(*messages.ActivityStoppingReply); ok {
				err = handleActivityStoppingReply(v, op)
			}

		// ActivityInvokeLocalReply
		case messagetypes.ActivityInvokeLocalReply:
			if v, ok := reply.(*messages.ActivityInvokeLocalReply); ok {
				err = handleActivityInvokeLocalReply(v, op)
			}

		// Undefined message type
		default:
			err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", reply.GetType())
			Logger.Error("Unhandled message type. Could not complete type assertion", zap.Error(err))
		}
	}

	return
}
