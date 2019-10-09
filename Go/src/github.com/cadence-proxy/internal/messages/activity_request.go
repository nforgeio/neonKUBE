//-----------------------------------------------------------------------------
// FILE:		activity_request.go
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

	// ActivityRequest is base type for all workflow requests
	// All workflow requests will inherit from ActivityRequest and
	// a ActivityRequest contains a ContextID, which is a int64 property
	//
	// A ActivityRequest contains a reference to a
	// ProxyReply struct in memory
	ActivityRequest struct {
		*ProxyRequest
	}

	// IActivityRequest is the interface that all workflow message requests
	// implement.  It allows access to a ActivityRequest's ContextID, a property
	// that all ActivityRequests share
	IActivityRequest interface {
		IProxyRequest
		GetContextID() int64
		SetContextID(value int64)
	}
)

// NewActivityRequest is the default constructor for a ActivityRequest
//
// returns *ActivityRequest -> a pointer to a newly initialized ActivityRequest
// in memory
func NewActivityRequest() *ActivityRequest {
	request := new(ActivityRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.Unspecified)
	request.SetReplyType(internal.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID gets the ContextId from a ActivityRequest's properties
// map.
//
// returns int64 -> the long representing a ActivityRequest's ContextId
func (request *ActivityRequest) GetContextID() int64 {
	return request.GetLongProperty("ContextId")
}

// SetContextID sets the ContextId in a ActivityRequest's properties map
//
// param value int64 -> int64 value to set as the ActivityRequest's ContextId
// in its properties map
func (request *ActivityRequest) SetContextID(value int64) {
	request.SetLongProperty("ContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *ActivityRequest) Clone() IProxyMessage {
	activityContextRequest := NewActivityRequest()
	var messageClone IProxyMessage = activityContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *ActivityRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IActivityRequest); ok {
		v.SetContextID(request.GetContextID())
	}
}
