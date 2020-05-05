//-----------------------------------------------------------------------------
// FILE:		workflow_cancel_request.go
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

	// WorkflowCancelRequest is WorkflowRequest of MessageType
	// WorkflowCancelRequest.
	//
	// A WorkflowCancelRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowCancelRequest will pass all of the given data and options
	// necessary to cancel a temporal workflow via the temporal client
	WorkflowCancelRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowCancelRequest is the default constructor for a WorkflowCancelRequest
//
// returns *WorkflowCancelRequest -> a reference to a newly initialized
// WorkflowCancelRequest in memory
func NewWorkflowCancelRequest() *WorkflowCancelRequest {
	request := new(WorkflowCancelRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowCancelRequest)
	request.SetReplyType(internal.WorkflowCancelReply)

	return request
}

// GetWorkflowID gets a WorkflowCancelRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's WorkflowID
func (request *WorkflowCancelRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowCancelRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's WorkflowID
func (request *WorkflowCancelRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowCancelRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's RunID
func (request *WorkflowCancelRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowCancelRequest's RunID value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowCancelRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetDomain gets a WorkflowCancelRequest's Domain value
// from its properties map. Optionally overrides the current client domain.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's Domain
func (request *WorkflowCancelRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowCancelRequest's Domain value
// in its properties map. Optionally overrides the current client domain.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowCancelRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowCancelRequest) Clone() IProxyMessage {
	workflowCancelRequest := NewWorkflowCancelRequest()
	var messageClone IProxyMessage = workflowCancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowCancelRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowCancelRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetDomain(request.GetDomain())
	}
}
