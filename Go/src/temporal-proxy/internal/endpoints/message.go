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

	"temporal-proxy/internal"
	"temporal-proxy/internal/messages"
)

var (

	// Mutex lock to keep race conditions from happening
	// when terminating the temporal-proxy
	terminateMu sync.Mutex
)

// MessageHandler accepts an http.PUT requests and parses the
// request body, converts it into an ProxyMessage object
// and talks through the uber temporal client to the temporal server,
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
	// back to the Neon.Temporal Lib
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
			reply = messages.CreateReplyMessage(request)
			err = fmt.Errorf("Panic: %s, MessageType: %s, RequestId: %d, ClientId: %d. %s",
				r,
				request.GetType().String(),
				request.GetRequestID(),
				request.GetClientID(),
				string(debug.Stack()))

			reply.Build(internal.NewTemporalError(err))

			Logger.Error("Panic", zap.Error(err))

			// send the reply
			var resp *http.Response
			resp, err = putToNeonTemporalClient(reply)
			if err != nil {
				Logger.Fatal(err.Error())
			}

			err = resp.Body.Close()
			if err != nil {
				return
			}
		}
	}()

	// check for a temporal server connection.
	// if it exists, then enter switch block to handle the
	// specified request type
	if err = verifyClientHelper(request, Clients.Get(request.GetClientID())); err != nil {
		reply = messages.CreateReplyMessage(request)
		reply.Build(internal.NewTemporalError(err))
	} else {

		// create a context for every request
		// handle the messages individually
		// based on their message type
		ctx := context.Background()
		switch request.GetType() {

		// -------------------------------------------------------------------------
		// Client message types

		// InitializeRequest
		case internal.InitializeRequest:
			if v, ok := request.(*messages.InitializeRequest); ok {
				reply = handleInitializeRequest(ctx, v)
			}

		// HeartbeatRequest
		case internal.HeartbeatRequest:
			if v, ok := request.(*messages.HeartbeatRequest); ok {
				reply = handleHeartbeatRequest(ctx, v)
			}

		// CancelRequest
		case internal.CancelRequest:
			if v, ok := request.(*messages.CancelRequest); ok {
				reply = handleCancelRequest(ctx, v)
			}

		// ConnectRequest
		case internal.ConnectRequest:
			if v, ok := request.(*messages.ConnectRequest); ok {
				reply = handleConnectRequest(ctx, v)
			}

		// DisconnectRequest
		case internal.DisconnectRequest:
			if v, ok := request.(*messages.DisconnectRequest); ok {
				reply = handleDisconnectRequest(ctx, v)
			}

		// NamespaceDescribeRequest
		case internal.NamespaceDescribeRequest:
			if v, ok := request.(*messages.NamespaceDescribeRequest); ok {
				reply = handleNamespaceDescribeRequest(ctx, v)
			}

		// NamespaceRegisterRequest
		case internal.NamespaceRegisterRequest:
			if v, ok := request.(*messages.NamespaceRegisterRequest); ok {
				reply = handleNamespaceRegisterRequest(ctx, v)
			}

		// NamespaceUpdateRequest
		case internal.NamespaceUpdateRequest:
			if v, ok := request.(*messages.NamespaceUpdateRequest); ok {
				reply = handleNamespaceUpdateRequest(ctx, v)
			}

		// TerminateRequest
		case internal.TerminateRequest:
			if v, ok := request.(*messages.TerminateRequest); ok {
				reply = handleTerminateRequest(ctx, v)
			}

		// NewWorkerRequest
		case internal.NewWorkerRequest:
			if v, ok := request.(*messages.NewWorkerRequest); ok {
				reply = handleNewWorkerRequest(ctx, v)
			}

		// StopWorkerRequest
		case internal.StopWorkerRequest:
			if v, ok := request.(*messages.StopWorkerRequest); ok {
				reply = handleStopWorkerRequest(ctx, v)
			}

		// PingRequest
		case internal.PingRequest:
			if v, ok := request.(*messages.PingRequest); ok {
				reply = handlePingRequest(ctx, v)
			}

		// DescribeTaskListRequest
		case internal.DescribeTaskListRequest:
			if v, ok := request.(*messages.DescribeTaskListRequest); ok {
				reply = handleDescribeTaskListRequest(ctx, v)
			}

		// -------------------------------------------------------------------------
		// Workflow message types

		// WorkflowRegisterRequest
		case internal.WorkflowRegisterRequest:
			if v, ok := request.(*messages.WorkflowRegisterRequest); ok {
				reply = handleWorkflowRegisterRequest(ctx, v)
			}

		// WorkflowExecuteRequest
		case internal.WorkflowExecuteRequest:
			if v, ok := request.(*messages.WorkflowExecuteRequest); ok {
				reply = handleWorkflowExecuteRequest(ctx, v)
			}

		// WorkflowCancelRequest
		case internal.WorkflowCancelRequest:
			if v, ok := request.(*messages.WorkflowCancelRequest); ok {
				reply = handleWorkflowCancelRequest(ctx, v)
			}

		// WorkflowTerminateRequest
		case internal.WorkflowTerminateRequest:
			if v, ok := request.(*messages.WorkflowTerminateRequest); ok {
				reply = handleWorkflowTerminateRequest(ctx, v)
			}

		// WorkflowSignalWithStartRequest
		case internal.WorkflowSignalWithStartRequest:
			if v, ok := request.(*messages.WorkflowSignalWithStartRequest); ok {
				reply = handleWorkflowSignalWithStartRequest(ctx, v)
			}

		// WorkflowSetCacheSizeRequest
		case internal.WorkflowSetCacheSizeRequest:
			if v, ok := request.(*messages.WorkflowSetCacheSizeRequest); ok {
				reply = handleWorkflowSetCacheSizeRequest(ctx, v)
			}

		// WorkflowQueryRequest
		case internal.WorkflowQueryRequest:
			if v, ok := request.(*messages.WorkflowQueryRequest); ok {
				reply = handleWorkflowQueryRequest(ctx, v)
			}

		// WorkflowMutableRequest
		case internal.WorkflowMutableRequest:
			if v, ok := request.(*messages.WorkflowMutableRequest); ok {
				reply = handleWorkflowMutableRequest(ctx, v)
			}

		// WorkflowDescribeExecutionRequest
		case internal.WorkflowDescribeExecutionRequest:
			if v, ok := request.(*messages.WorkflowDescribeExecutionRequest); ok {
				reply = handleWorkflowDescribeExecutionRequest(ctx, v)
			}

		// WorkflowGetResultRequest
		case internal.WorkflowGetResultRequest:
			if v, ok := request.(*messages.WorkflowGetResultRequest); ok {
				reply = handleWorkflowGetResultRequest(ctx, v)
			}

		// WorkflowSignalSubscribeRequest
		case internal.WorkflowSignalSubscribeRequest:
			if v, ok := request.(*messages.WorkflowSignalSubscribeRequest); ok {
				reply = handleWorkflowSignalSubscribeRequest(ctx, v)
			}

		// WorkflowSignalRequest
		case internal.WorkflowSignalRequest:
			if v, ok := request.(*messages.WorkflowSignalRequest); ok {
				reply = handleWorkflowSignalRequest(ctx, v)
			}

		// WorkflowHasLastResultRequest
		case internal.WorkflowHasLastResultRequest:
			if v, ok := request.(*messages.WorkflowHasLastResultRequest); ok {
				reply = handleWorkflowHasLastResultRequest(ctx, v)
			}

		// WorkflowGetLastResultRequest
		case internal.WorkflowGetLastResultRequest:
			if v, ok := request.(*messages.WorkflowGetLastResultRequest); ok {
				reply = handleWorkflowGetLastResultRequest(ctx, v)
			}

		// WorkflowDisconnectContextRequest
		case internal.WorkflowDisconnectContextRequest:
			if v, ok := request.(*messages.WorkflowDisconnectContextRequest); ok {
				reply = handleWorkflowDisconnectContextRequest(ctx, v)
			}

		// WorkflowGetTimeRequest
		case internal.WorkflowGetTimeRequest:
			if v, ok := request.(*messages.WorkflowGetTimeRequest); ok {
				reply = handleWorkflowGetTimeRequest(ctx, v)
			}

		// WorkflowSleepRequest
		case internal.WorkflowSleepRequest:
			if v, ok := request.(*messages.WorkflowSleepRequest); ok {
				reply = handleWorkflowSleepRequest(ctx, v)
			}

		// WorkflowExecuteChildRequest
		case internal.WorkflowExecuteChildRequest:
			if v, ok := request.(*messages.WorkflowExecuteChildRequest); ok {
				reply = handleWorkflowExecuteChildRequest(ctx, v)
			}

		// WorkflowWaitForChildRequest
		case internal.WorkflowWaitForChildRequest:
			if v, ok := request.(*messages.WorkflowWaitForChildRequest); ok {
				reply = handleWorkflowWaitForChildRequest(ctx, v)
			}

		// WorkflowSignalChildRequest
		case internal.WorkflowSignalChildRequest:
			if v, ok := request.(*messages.WorkflowSignalChildRequest); ok {
				reply = handleWorkflowSignalChildRequest(ctx, v)
			}

		// WorkflowCancelChildRequest
		case internal.WorkflowCancelChildRequest:
			if v, ok := request.(*messages.WorkflowCancelChildRequest); ok {
				reply = handleWorkflowCancelChildRequest(ctx, v)
			}

		// WorkflowSetQueryHandlerRequest
		case internal.WorkflowSetQueryHandlerRequest:
			if v, ok := request.(*messages.WorkflowSetQueryHandlerRequest); ok {
				reply = handleWorkflowSetQueryHandlerRequest(ctx, v)
			}

		// WorkflowGetVersionRequest
		case internal.WorkflowGetVersionRequest:
			if v, ok := request.(*messages.WorkflowGetVersionRequest); ok {
				reply = handleWorkflowGetVersionRequest(ctx, v)
			}

		// WorkflowQueueNewRequest
		case internal.WorkflowQueueNewRequest:
			if v, ok := request.(*messages.WorkflowQueueNewRequest); ok {
				reply = handleWorkflowQueueNewRequest(ctx, v)
			}

		// WorkflowQueueWriteRequest
		case internal.WorkflowQueueWriteRequest:
			if v, ok := request.(*messages.WorkflowQueueWriteRequest); ok {
				reply = handleWorkflowQueueWriteRequest(ctx, v)
			}

		// WorkflowQueueReadRequest
		case internal.WorkflowQueueReadRequest:
			if v, ok := request.(*messages.WorkflowQueueReadRequest); ok {
				reply = handleWorkflowQueueReadRequest(ctx, v)
			}

		// WorkflowQueueCloseRequest
		case internal.WorkflowQueueCloseRequest:
			if v, ok := request.(*messages.WorkflowQueueCloseRequest); ok {
				reply = handleWorkflowQueueCloseRequest(ctx, v)
			}

		// -------------------------------------------------------------------------
		// Activity message types

		// ActivityExecuteRequest
		case internal.ActivityExecuteRequest:
			if v, ok := request.(*messages.ActivityExecuteRequest); ok {
				reply = handleActivityExecuteRequest(ctx, v)
			}

		// ActivityRegisterRequest
		case internal.ActivityRegisterRequest:
			if v, ok := request.(*messages.ActivityRegisterRequest); ok {
				reply = handleActivityRegisterRequest(ctx, v)
			}

		// ActivityHasHeartbeatDetailsRequest
		case internal.ActivityHasHeartbeatDetailsRequest:
			if v, ok := request.(*messages.ActivityHasHeartbeatDetailsRequest); ok {
				reply = handleActivityHasHeartbeatDetailsRequest(ctx, v)
			}

		// ActivityGetHeartbeatDetailsRequest
		case internal.ActivityGetHeartbeatDetailsRequest:
			if v, ok := request.(*messages.ActivityGetHeartbeatDetailsRequest); ok {
				reply = handleActivityGetHeartbeatDetailsRequest(ctx, v)
			}

		// ActivityRecordHeartbeatRequest
		case internal.ActivityRecordHeartbeatRequest:
			if v, ok := request.(*messages.ActivityRecordHeartbeatRequest); ok {
				reply = handleActivityRecordHeartbeatRequest(ctx, v)
			}

		// ActivityGetInfoRequest
		case internal.ActivityGetInfoRequest:
			if v, ok := request.(*messages.ActivityGetInfoRequest); ok {
				reply = handleActivityGetInfoRequest(ctx, v)
			}

		// ActivityCompleteRequest
		case internal.ActivityCompleteRequest:
			if v, ok := request.(*messages.ActivityCompleteRequest); ok {
				reply = handleActivityCompleteRequest(ctx, v)
			}

		// ActivityExecuteLocalRequest
		case internal.ActivityExecuteLocalRequest:
			if v, ok := request.(*messages.ActivityExecuteLocalRequest); ok {
				reply = handleActivityExecuteLocalRequest(ctx, v)
			}

		// ActivityStartRequest
		case internal.ActivityStartRequest:
			if v, ok := request.(*messages.ActivityStartRequest); ok {
				reply = handleActivityStartRequest(ctx, v)
			}

		// ActivityGetResultRequest
		case internal.ActivityGetResultRequest:
			if v, ok := request.(*messages.ActivityGetResultRequest); ok {
				reply = handleActivityGetResultRequest(ctx, v)
			}

		// ActivityStartLocalRequest
		case internal.ActivityStartLocalRequest:
			if v, ok := request.(*messages.ActivityStartLocalRequest); ok {
				reply = handleActivityStartLocalRequest(ctx, v)
			}

		// ActivityGetLocalResultRequest
		case internal.ActivityGetLocalResultRequest:
			if v, ok := request.(*messages.ActivityGetLocalResultRequest); ok {
				reply = handleActivityGetLocalResultRequest(ctx, v)
			}

		// Undefined message type
		default:
			e := fmt.Errorf("unhandled message type. could not complete type assertion for type %d", request.GetType())

			// set the reply
			reply = messages.NewProxyReply()
			reply.SetRequestID(request.GetRequestID())
			reply.Build(internal.NewTemporalError(e, internal.CustomError))
		}
	}

	// send the reply
	var resp *http.Response
	resp, err = putToNeonTemporalClient(reply)
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
				string(debug.Stack()))

			Logger.Error("Panic", zap.Error(err))
		}

		Operations.Remove(requestID)
	}()

	// check to make sure that the operation exists
	op := Operations.Get(reply.GetRequestID())

	if op == nil {
		err = internal.ErrEntityNotExist
	} else {

		// handle the messages individually based on their message type
		switch reply.GetType() {

		// LogReply
		case internal.LogReply:
			if v, ok := reply.(*messages.LogReply); ok {
				err = handleLogReply(v, op)
			}

		// WorkflowInvokeReply
		case internal.WorkflowInvokeReply:
			if v, ok := reply.(*messages.WorkflowInvokeReply); ok {
				err = handleWorkflowInvokeReply(v, op)
			}

		// WorkflowSignalInvokeReply
		case internal.WorkflowSignalInvokeReply:
			if v, ok := reply.(*messages.WorkflowSignalInvokeReply); ok {
				err = handleWorkflowSignalInvokeReply(v, op)
			}

		// WorkflowQueryInvokeReply
		case internal.WorkflowQueryInvokeReply:
			if v, ok := reply.(*messages.WorkflowQueryInvokeReply); ok {
				err = handleWorkflowQueryInvokeReply(v, op)
			}

		// WorkflowFutureReadyReply
		case internal.WorkflowFutureReadyReply:
			if v, ok := reply.(*messages.WorkflowFutureReadyReply); ok {
				err = handleWorkflowFutureReadyReply(v, op)
			}

		// ActivityInvokeReply
		case internal.ActivityInvokeReply:
			if v, ok := reply.(*messages.ActivityInvokeReply); ok {
				err = handleActivityInvokeReply(v, op)
			}

		// ActivityStoppingReply
		case internal.ActivityStoppingReply:
			if v, ok := reply.(*messages.ActivityStoppingReply); ok {
				err = handleActivityStoppingReply(v, op)
			}

		// ActivityInvokeLocalReply
		case internal.ActivityInvokeLocalReply:
			if v, ok := reply.(*messages.ActivityInvokeLocalReply); ok {
				err = handleActivityInvokeLocalReply(v, op)
			}

		// Undefined message type
		default:
			err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", reply.GetType())
		}
	}

	return
}
