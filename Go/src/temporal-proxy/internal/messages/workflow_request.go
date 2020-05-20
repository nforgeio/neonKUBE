//-----------------------------------------------------------------------------
// FILE:		workflow_request.go
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

	// WorkflowRequest is base type for all workflow requests
	// All workflow requests will inherit from WorkflowRequest and
	// a WorkflowRequest contains a ContextID, which is a int64 property
	//
	// A WorkflowRequest contains a reference to a
	// ProxyReply struct in memory
	WorkflowRequest struct {
		*ProxyRequest
	}

	// IWorkflowRequest is the interface that all workflow message requests
	// implement.  It allows access to a WorkflowRequest's ContextID, a property
	// that all WorkflowRequests share
	IWorkflowRequest interface {
		IProxyRequest
		GetContextID() int64
		SetContextID(value int64)
	}
)

// NewWorkflowRequest is the default constructor for a WorkflowRequest
//
// returns *WorkflowRequest -> a pointer to a newly initialized WorkflowRequest
// in memory
func NewWorkflowRequest() *WorkflowRequest {
	request := new(WorkflowRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.Unspecified)
	request.SetReplyType(internal.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID gets the ContextId from a WorkflowRequest's properties
// map.
//
// returns int64 -> the long representing a WorkflowRequest's ContextId
func (request *WorkflowRequest) GetContextID() int64 {
	return request.GetLongProperty("ContextId")
}

// SetContextID sets the ContextId in a WorkflowRequest's properties map
//
// param value int64 -> int64 value to set as the WorkflowRequest's ContextId
// in its properties map
func (request *WorkflowRequest) SetContextID(value int64) {
	request.SetLongProperty("ContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowRequest) Clone() IProxyMessage {
	workflowContextRequest := NewWorkflowRequest()
	var messageClone IProxyMessage = workflowContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IWorkflowRequest); ok {
		v.SetContextID(request.GetContextID())
	}
}
