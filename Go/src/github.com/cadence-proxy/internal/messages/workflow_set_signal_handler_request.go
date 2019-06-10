//-----------------------------------------------------------------------------
// FILE:		workflow_set_signal_handler_request.go
// CONTRIBUTOR: John C Burnes
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetSignalHandlerRequest is WorkflowRequest of MessageType
	// WorkflowSetSignalHandlerRequest.
	//
	// A WorkflowSetSignalHandlerRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Sends a signal to a running workflow.
	WorkflowSetSignalHandlerRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSetSignalHandlerRequest is the default constructor for a WorkflowSetSignalHandlerRequest
//
// returns *WorkflowSetSignalHandlerRequest -> a reference to a newly initialized
// WorkflowSetSignalHandlerRequest in memory
func NewWorkflowSetSignalHandlerRequest() *WorkflowSetSignalHandlerRequest {
	request := new(WorkflowSetSignalHandlerRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSetSignalHandlerRequest)
	request.SetReplyType(messagetypes.WorkflowSetSignalHandlerReply)

	return request
}

// GetSignalName gets a WorkflowSetSignalHandlerRequest's SignalName value
// from its properties map. Identifies the signal.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSetSignalHandlerRequest's SignalName
func (request *WorkflowSetSignalHandlerRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSetSignalHandlerRequest's SignalName value
// in its properties map. Identifies the signal.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSetSignalHandlerRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSetSignalHandlerRequest) Clone() IProxyMessage {
	workflowSetSignalHandlerRequest := NewWorkflowSetSignalHandlerRequest()
	var messageClone IProxyMessage = workflowSetSignalHandlerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSetSignalHandlerRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSetSignalHandlerRequest); ok {
		v.SetSignalName(request.GetSignalName())
	}
}
