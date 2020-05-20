//-----------------------------------------------------------------------------
// FILE:		workflow_wait_for_child_request.go
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

	// WorkflowWaitForChildRequest is WorkflowRequest of MessageType
	// WorkflowWaitForChildRequest.
	//
	// A WorkflowWaitForChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Waits for a child workflow to complete.
	WorkflowWaitForChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowWaitForChildRequest is the default constructor for a WorkflowWaitForChildRequest
//
// returns *WorkflowWaitForChildRequest -> a reference to a newly initialized
// WorkflowWaitForChildRequest in memory
func NewWorkflowWaitForChildRequest() *WorkflowWaitForChildRequest {
	request := new(WorkflowWaitForChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowWaitForChildRequest)
	request.SetReplyType(internal.WorkflowWaitForChildReply)

	return request
}

// GetChildID gets a WorkflowWaitForChildRequest's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowWaitForChildRequest's ChildID
func (request *WorkflowWaitForChildRequest) GetChildID() int64 {
	return request.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowWaitForChildRequest's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowWaitForChildRequest's ChildID to be set in the
// WorkflowWaitForChildRequest's properties map.
func (request *WorkflowWaitForChildRequest) SetChildID(value int64) {
	request.SetLongProperty("ChildId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowWaitForChildRequest) Clone() IProxyMessage {
	workflowWaitForChildRequest := NewWorkflowWaitForChildRequest()
	var messageClone IProxyMessage = workflowWaitForChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowWaitForChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowWaitForChildRequest); ok {
		v.SetChildID(request.GetChildID())
	}
}
