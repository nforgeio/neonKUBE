//-----------------------------------------------------------------------------
// FILE:		workflow_list_open_executions_request.go
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

	// WorkflowListOpenExecutionsRequest is WorkflowRequest of MessageType
	// WorkflowListOpenExecutionsRequest.
	//
	// A WorkflowListOpenExecutionsRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowListOpenExecutionsRequest will pass all of the given data and options
	// necessary to list open cadence workflow executions
	WorkflowListOpenExecutionsRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowListOpenExecutionsRequest is the default constructor for a WorkflowListOpenExecutionsRequest
//
// returns *WorkflowListOpenExecutionsRequest -> a reference to a newly initialized
// WorkflowListOpenExecutionsRequest in memory
func NewWorkflowListOpenExecutionsRequest() *WorkflowListOpenExecutionsRequest {
	request := new(WorkflowListOpenExecutionsRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowListOpenExecutionsRequest)
	request.SetReplyType(internal.WorkflowListOpenExecutionsReply)

	return request
}

// GetMaximumPageSize gets a WorkflowListOpenExecutionsRequest's MaximumPageSize value
// from its properties map.
//
// returns int32 -> int32 holding the value
// of a WorkflowListOpenExecutionsRequest's MaximumPageSize
func (request *WorkflowListOpenExecutionsRequest) GetMaximumPageSize() int32 {
	return request.GetIntProperty("MaximumPageSize")
}

// SetMaximumPageSize sets a WorkflowListOpenExecutionsRequest's MaximumPageSize value
// in its properties map.
//
// param value int32 -> int32 holding the value
// to be set in the properties map
func (request *WorkflowListOpenExecutionsRequest) SetMaximumPageSize(value int32) {
	request.SetIntProperty("MaximumPageSize", value)
}

// GetDomain gets a WorkflowListOpenExecutionsRequest's Domain value
// from its properties map. Optionally overrides the current client domain.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowListOpenExecutionsRequest's Domain
func (request *WorkflowListOpenExecutionsRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowListOpenExecutionsRequest's Domain value
// in its properties map. Optionally overrides the current client domain.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowListOpenExecutionsRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowListOpenExecutionsRequest) Clone() IProxyMessage {
	workflowListOpenExecutionsRequest := NewWorkflowListOpenExecutionsRequest()
	var messageClone IProxyMessage = workflowListOpenExecutionsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowListOpenExecutionsRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowListOpenExecutionsRequest); ok {
		v.SetMaximumPageSize(request.GetMaximumPageSize())
		v.SetDomain(request.GetDomain())
	}
}
