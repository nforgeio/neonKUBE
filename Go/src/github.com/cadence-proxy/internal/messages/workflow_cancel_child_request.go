//-----------------------------------------------------------------------------
// FILE:		workflow_cancel_child_request.go
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
	internal "github.com/cadence-proxy/internal"
)

type (

	// WorkflowCancelChildRequest is WorkflowRequest of MessageType
	// WorkflowCancelChildRequest.
	//
	// A WorkflowCancelChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Cancels a child workflow.
	WorkflowCancelChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowCancelChildRequest is the default constructor for a WorkflowCancelChildRequest
//
// returns *WorkflowCancelChildRequest -> a reference to a newly initialized
// WorkflowCancelChildRequest in memory
func NewWorkflowCancelChildRequest() *WorkflowCancelChildRequest {
	request := new(WorkflowCancelChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowCancelChildRequest)
	request.SetReplyType(internal.WorkflowCancelChildReply)

	return request
}

// GetChildID gets a WorkflowCancelChildRequest's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowCancelChildRequest's ChildID
func (request *WorkflowCancelChildRequest) GetChildID() int64 {
	return request.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowCancelChildRequest's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowCancelChildRequest's ChildID to be set in the
// WorkflowCancelChildRequest's properties map.
func (request *WorkflowCancelChildRequest) SetChildID(value int64) {
	request.SetLongProperty("ChildId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowCancelChildRequest) Clone() IProxyMessage {
	workflowCancelChildRequest := NewWorkflowCancelChildRequest()
	var messageClone IProxyMessage = workflowCancelChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowCancelChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowCancelChildRequest); ok {
		v.SetChildID(request.GetChildID())
	}
}
