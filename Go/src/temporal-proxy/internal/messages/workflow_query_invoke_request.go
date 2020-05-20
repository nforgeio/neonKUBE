//-----------------------------------------------------------------------------
// FILE:		workflow_query_invoke_request.go
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
	proxytemporal "temporal-proxy/internal/temporal"
)

type (

	// WorkflowQueryInvokeRequest is WorkflowRequest of MessageType
	// WorkflowQueryInvokeRequest.
	//
	// A WorkflowQueryInvokeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowQueryInvokeRequest will pass all of the given data and options
	// necessary to query a running workflow.
	WorkflowQueryInvokeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueryInvokeRequest is the default constructor for a WorkflowQueryInvokeRequest
//
// returns *WorkflowQueryInvokeRequest -> a reference to a newly initialized
// WorkflowQueryInvokeRequest in memory
func NewWorkflowQueryInvokeRequest() *WorkflowQueryInvokeRequest {
	request := new(WorkflowQueryInvokeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueryInvokeRequest)
	request.SetReplyType(internal.WorkflowQueryInvokeReply)

	return request
}

// GetQueryName gets a WorkflowQueryInvokeRequest's QueryInvokeName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryInvokeRequest's QueryName
func (request *WorkflowQueryInvokeRequest) GetQueryName() *string {
	return request.GetStringProperty("QueryName")
}

// SetQueryName sets a WorkflowQueryInvokeRequest's QueryName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowQueryInvokeRequest) SetQueryName(value *string) {
	request.SetStringProperty("QueryName", value)
}

// GetQueryArgs gets a WorkflowQueryInvokeRequest's QueryArgs field
// from its properties map.  QueryArgs is a []byte holding the arguments
// for querying a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowQueryInvokeRequest) GetQueryArgs() []byte {
	return request.GetBytesProperty("QueryArgs")
}

// SetQueryArgs sets an WorkflowQueryInvokeRequest's QueryArgs field
// from its properties map.  QueryArgs is a []byte holding the arguments
// for querying a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowQueryInvokeRequest) SetQueryArgs(value []byte) {
	request.SetBytesProperty("QueryArgs", value)
}

// GetReplayStatus gets the ReplayStatus from a WorkflowQueryInvokeRequest's properties
// map. For workflow requests related to an executing workflow,
// this will indicate the current history replay state.
//
// returns proxytemporal.ReplayStatus -> the current history replay
// state of a workflow
func (request *WorkflowQueryInvokeRequest) GetReplayStatus() proxytemporal.ReplayStatus {
	replayStatusPtr := request.GetStringProperty("ReplayStatus")
	if replayStatusPtr == nil {
		return proxytemporal.ReplayStatusUnspecified
	}
	replayStatus := proxytemporal.StringToReplayStatus(*replayStatusPtr)

	return replayStatus
}

// SetReplayStatus sets the ReplayStatus in a WorkflowQueryInvokeRequest's properties
// map. For workflow requests related to an executing workflow,
// this will indicate the current history replay state.
//
// param value proxytemporal.ReplayStatus -> the current history replay
// state of a workflow
func (request *WorkflowQueryInvokeRequest) SetReplayStatus(value proxytemporal.ReplayStatus) {
	status := value.String()
	request.SetStringProperty("ReplayStatus", &status)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueryInvokeRequest) Clone() IProxyMessage {
	workflowQueryInvokeRequest := NewWorkflowQueryInvokeRequest()
	var messageClone IProxyMessage = workflowQueryInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueryInvokeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueryInvokeRequest); ok {
		v.SetQueryName(request.GetQueryName())
		v.SetQueryArgs(request.GetQueryArgs())
		v.SetReplayStatus(request.GetReplayStatus())
	}
}
