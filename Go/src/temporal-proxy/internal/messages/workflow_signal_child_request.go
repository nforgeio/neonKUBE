//-----------------------------------------------------------------------------
// FILE:		workflow_signal_child_request.go
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

	// WorkflowSignalChildRequest is WorkflowRequest of MessageType
	// WorkflowSignalChildRequest.
	//
	// A WorkflowSignalChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Sends a signal to a child workflow.
	WorkflowSignalChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalChildRequest is the default constructor for a WorkflowSignalChildRequest
//
// returns *WorkflowSignalChildRequest -> a reference to a newly initialized
// WorkflowSignalChildRequest in memory
func NewWorkflowSignalChildRequest() *WorkflowSignalChildRequest {
	request := new(WorkflowSignalChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSignalChildRequest)
	request.SetReplyType(internal.WorkflowSignalChildReply)

	return request
}

// GetChildID gets a WorkflowSignalChildRequest's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowSignalChildRequest's ChildID
func (request *WorkflowSignalChildRequest) GetChildID() int64 {
	return request.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowSignalChildRequest's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowSignalChildRequest's ChildID to be set in the
// WorkflowSignalChildRequest's properties map.
func (request *WorkflowSignalChildRequest) SetChildID(value int64) {
	request.SetLongProperty("ChildId", value)
}

// GetSignalName gets a WorkflowSignalChildRequest's SignalName value
// from its properties map. Identifies the signal.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalChildRequest's SignalName
func (request *WorkflowSignalChildRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalChildRequest's SignalName value
// in its properties map. Identifies the signal.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalChildRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalChildRequest's SignalArgs field
// from its properties map.  Optionally specifies the signal arguments.
//
// returns []byte -> a []byte representing the optional signal arguments
// for signaling a child workflow
func (request *WorkflowSignalChildRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalChildRequest's SignalArgs field
// from its properties map.  Optionally specifies the signal arguments.
//
// param value []byte -> []byte representing the optional signal arguments
// for signaling a child workflow
func (request *WorkflowSignalChildRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalChildRequest) Clone() IProxyMessage {
	workflowSignalChildRequest := NewWorkflowSignalChildRequest()
	var messageClone IProxyMessage = workflowSignalChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalChildRequest); ok {
		v.SetChildID(request.GetChildID())
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
	}
}
