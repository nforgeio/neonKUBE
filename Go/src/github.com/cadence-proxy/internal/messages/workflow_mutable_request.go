//-----------------------------------------------------------------------------
// FILE:		workflow_mutable_request.go
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableRequest is WorkflowRequest of MessageType
	// WorkflowMutableRequest.
	//
	// A WorkflowMutableRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowMutableRequest will pass all of the given data
	// necessary to invoke a cadence workflow instance via the cadence client
	WorkflowMutableRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowMutableRequest is the default constructor for a WorkflowMutableRequest
//
// returns *WorkflowMutableRequest -> a reference to a newly initialized
// WorkflowMutableRequest in memory
func NewWorkflowMutableRequest() *WorkflowMutableRequest {
	request := new(WorkflowMutableRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowMutableRequest)
	request.SetReplyType(messagetypes.WorkflowMutableReply)

	return request
}

// GetMutableID gets a WorkflowMutableRequest's MutableID value
// from its properties map. Identifies the mutable value to be returned.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowMutableRequest's MutableID
func (request *WorkflowMutableRequest) GetMutableID() *string {
	return request.GetStringProperty("MutableId")
}

// SetMutableID sets an WorkflowMutableRequest's MutableID value
// in its properties map. Identifies the mutable value to be returned.
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowMutableRequest's MutableID
func (request *WorkflowMutableRequest) SetMutableID(value *string) {
	request.SetStringProperty("MutableId", value)
}

// GetResult gets a WorkflowMutableRequest's Result value
// from its properties map. The result of the mutable value to be set.
//
// returns []byte -> the result encoded as a []byte
// of a WorkflowMutableRequest's Result
func (request *WorkflowMutableRequest) GetResult() []byte {
	return request.GetBytesProperty("Result")
}

// SetResult sets an WorkflowMutableRequest's Result value
// in its properties map. The result of the mutable value to be set.
//
// param value []byte -> the result encoded as a []byte
func (request *WorkflowMutableRequest) SetResult(value []byte) {
	request.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowMutableRequest) Clone() IProxyMessage {
	workflowMutableRequest := NewWorkflowMutableRequest()
	var messageClone IProxyMessage = workflowMutableRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowMutableRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowMutableRequest); ok {
		v.SetMutableID(request.GetMutableID())
		v.SetResult(request.GetResult())
	}
}
