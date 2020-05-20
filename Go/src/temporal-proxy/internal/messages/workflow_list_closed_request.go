//-----------------------------------------------------------------------------
// FILE:		workflow_list_closed_request.go
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
	"fmt"

	internal "temporal-proxy/internal"
)

type (

	// WorkflowListClosedRequest is WorkflowRequest of MessageType
	// WorkflowListClosedRequest.
	//
	// A WorkflowListClosedRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowListClosedRequest will pass all of the given data
	// necessary to list the closed temporal workflow execution instances
	WorkflowListClosedRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowListClosedRequest is the default constructor for a WorkflowListClosedRequest
//
// returns *WorkflowListClosedRequest -> a reference to a newly initialized
// WorkflowListClosedRequest in memory
func NewWorkflowListClosedRequest() *WorkflowListClosedRequest {
	request := new(WorkflowListClosedRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowListClosedRequest)
	request.SetReplyType(internal.WorkflowListClosedReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowListClosedRequest) Clone() IProxyMessage {
	WorkflowListClosedRequest := NewWorkflowListClosedRequest()
	var messageClone IProxyMessage = WorkflowListClosedRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowListClosedRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowListClosedRequest); ok {
		fmt.Printf("TODO: JACK -- IMPLEMENT %v", v)
	}
}
