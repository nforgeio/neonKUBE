//-----------------------------------------------------------------------------
// FILE:		workflow_signal_invoke_request.go
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

package messages

import (
	proxyworkflow "temporal-proxy/internal/temporal/workflow"
	internal "temporal-proxy/internal"
)

type (

	// WorkflowSignalInvokeRequest is WorkflowRequest of MessageType
	// WorkflowSignalInvokeRequest.
	//
	// A WorkflowSignalInvokeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalInvokeRequest sends a received signal to a running workflow.
	WorkflowSignalInvokeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalInvokeRequest is the default constructor for a WorkflowSignalInvokeRequest
//
// returns *WorkflowSignalInvokeRequest -> a reference to a newly initialized
// WorkflowSignalInvokeRequest in memory
func NewWorkflowSignalInvokeRequest() *WorkflowSignalInvokeRequest {
	request := new(WorkflowSignalInvokeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSignalInvokeRequest)
	request.SetReplyType(internal.WorkflowSignalInvokeReply)

	return request
}

// GetSignalName gets a WorkflowSignalInvokeRequest's SignalName value
// from its properties map. Identifies the signal.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalInvokeRequest's SignalName
func (request *WorkflowSignalInvokeRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalInvokeRequest's SignalName value
// in its properties map. Identifies the signal.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalInvokeRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalInvokeRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalInvokeRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalInvokeRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalInvokeRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// GetReplayStatus gets the ReplayStatus from a WorkflowSignalInvokeRequest's properties
// map. For workflow requests related to an executing workflow,
// this will indicate the current history replay state.
//
// returns proxyworkflow.ReplayStatus -> the current history replay
// state of a workflow
func (request *WorkflowSignalInvokeRequest) GetReplayStatus() proxyworkflow.ReplayStatus {
	replayStatusPtr := request.GetStringProperty("ReplayStatus")
	if replayStatusPtr == nil {
		return proxyworkflow.ReplayStatusUnspecified
	}
	replayStatus := proxyworkflow.StringToReplayStatus(*replayStatusPtr)

	return replayStatus
}

// SetReplayStatus sets the ReplayStatus in a WorkflowSignalInvokeRequest's properties
// map. For workflow requests related to an executing workflow,
// this will indicate the current history replay state.
//
// param value proxyworkflow.ReplayStatus -> the current history replay
// state of a workflow
func (request *WorkflowSignalInvokeRequest) SetReplayStatus(value proxyworkflow.ReplayStatus) {
	status := value.String()
	request.SetStringProperty("ReplayStatus", &status)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalInvokeRequest) Clone() IProxyMessage {
	workflowSignalInvokeRequest := NewWorkflowSignalInvokeRequest()
	var messageClone IProxyMessage = workflowSignalInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalInvokeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalInvokeRequest); ok {
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
		v.SetReplayStatus(request.GetReplayStatus())
	}
}
