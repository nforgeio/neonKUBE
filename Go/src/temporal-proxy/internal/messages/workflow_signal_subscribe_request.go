//-----------------------------------------------------------------------------
// FILE:		workflow_signal_subscribe_request.go
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
	internal "temporal-proxy/internal"
)

type (

	// WorkflowSignalSubscribeRequest is WorkflowRequest of MessageType
	// WorkflowSignalSubscribeRequest.
	//
	// A WorkflowSignalSubscribeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalSubscribeRequest will pass all of the given information
	// necessary to subscribe a workflow to a named signal
	WorkflowSignalSubscribeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalSubscribeRequest is the default constructor for a WorkflowSignalSubscribeRequest
//
// returns *WorkflowSignalSubscribeRequest -> a reference to a newly initialized
// WorkflowSignalSubscribeRequest in memory
func NewWorkflowSignalSubscribeRequest() *WorkflowSignalSubscribeRequest {
	request := new(WorkflowSignalSubscribeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSignalSubscribeRequest)
	request.SetReplyType(internal.WorkflowSignalSubscribeReply)

	return request
}

// GetSignalName gets a WorkflowSignalSubscribeRequest's SignalName value
// from its properties map. Identifies the signal being subscribed.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalSubscribeRequest's SignalName
func (request *WorkflowSignalSubscribeRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalSubscribeRequest's SignalName value
// in its properties map. Identifies the signal being subscribed.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalSubscribeRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalSubscribeRequest) Clone() IProxyMessage {
	workflowSignalSubscribeRequest := NewWorkflowSignalSubscribeRequest()
	var messageClone IProxyMessage = workflowSignalSubscribeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalSubscribeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalSubscribeRequest); ok {
		v.SetSignalName(request.GetSignalName())
	}
}
