//-----------------------------------------------------------------------------
// FILE:		activity_invoke_request.go
// CONTRIBUTOR: John C Burnes
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

	// ActivityInvokeRequest is an ActivityRequest of MessageType
	// ActivityInvokeRequest.
	//
	// A ActivityInvokeRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Sent to a worker, instructing it to begin executing
	// a workflow activity.
	ActivityInvokeRequest struct {
		*ActivityRequest
	}
)

// NewActivityInvokeRequest is the default constructor for a ActivityInvokeRequest
//
// returns *ActivityInvokeRequest -> a pointer to a newly initialized ActivityInvokeRequest
// in memory
func NewActivityInvokeRequest() *ActivityInvokeRequest {
	request := new(ActivityInvokeRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityInvokeRequest)
	request.SetReplyType(messagetypes.ActivityInvokeReply)

	return request
}

// GetArgs gets a ActivityInvokeRequest's Args field
// from its properties map.  Optionally specifies the activity
// arguments encoded as a byte array.
//
// returns []byte -> []byte representing workflow activity parameters or arguments
func (request *ActivityInvokeRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityInvokeRequest's Args field
// from its properties map.  Optionally specifies the activity
// arguments encoded as a byte array.
//
// param value []byte -> []byte representing workflow activity parameters or arguments
func (request *ActivityInvokeRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityInvokeRequest) Clone() IProxyMessage {
	activityInvokeRequest := NewActivityInvokeRequest()
	var messageClone IProxyMessage = activityInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityInvokeRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityInvokeRequest); ok {
		v.SetArgs(request.GetArgs())
	}
}
