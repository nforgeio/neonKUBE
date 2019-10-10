//-----------------------------------------------------------------------------
// FILE:		workflow_set_cache_size_request.go
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

	// WorkflowSetCacheSizeRequest is WorkflowRequest of MessageType
	// WorkflowSetCacheSizeRequest.
	//
	// A WorkflowSetCacheSizeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSetCacheSizeRequest sets the maximum number of bytes the client will use
	/// to cache the history of a sticky workflow on a workflow worker as a performance
	/// optimization.  When this is exceeded for a workflow, its full history will
	/// need to be retrieved from the Cadence cluster the next time the workflow
	/// instance is assigned to a worker.
	WorkflowSetCacheSizeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSetCacheSizeRequest is the default constructor for a WorkflowSetCacheSizeRequest
//
// returns *WorkflowSetCacheSizeRequest -> a reference to a newly initialized
// WorkflowSetCacheSizeRequest in memory
func NewWorkflowSetCacheSizeRequest() *WorkflowSetCacheSizeRequest {
	request := new(WorkflowSetCacheSizeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSetCacheSizeRequest)
	request.SetReplyType(internal.WorkflowSetCacheSizeReply)

	return request
}

// GetSize gets a WorkflowSetCacheSizeRequest's Size value
// from its properties map.  Specifies the maximum number of bytes used for
// caching sticky workflows.
//
// returns int -> int specifying the maximum number of bytes used for caching
// sticky workflows.cache Size
func (request *WorkflowSetCacheSizeRequest) GetSize() int {
	return int(request.GetIntProperty("Size"))
}

// SetSize sets a WorkflowSetCacheSizeRequest's Size value
// in its properties map
//
// param value int -> int specifying the maximum number of bytes used for caching
// sticky workflows.cache Size
func (request *WorkflowSetCacheSizeRequest) SetSize(value int) {
	request.SetIntProperty("Size", int32(value))
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSetCacheSizeRequest) Clone() IProxyMessage {
	workflowSetCacheSizeRequest := NewWorkflowSetCacheSizeRequest()
	var messageClone IProxyMessage = workflowSetCacheSizeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSetCacheSizeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSetCacheSizeRequest); ok {
		v.SetSize(request.GetSize())
	}
}
