//-----------------------------------------------------------------------------
// FILE:		activity_register_request.go
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

	// ActivityRegisterRequest is an ActivityRequest of MessageType
	// ActivityRegisterRequest.
	//
	// A ActivityRegisterRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Registers an activity with the temporal server
	ActivityRegisterRequest struct {
		*ActivityRequest
	}
)

// NewActivityRegisterRequest is the default constructor for a ActivityRegisterRequest
//
// returns *ActivityRegisterRequest -> a pointer to a newly initialized ActivityRegisterRequest
// in memory
func NewActivityRegisterRequest() *ActivityRegisterRequest {
	request := new(ActivityRegisterRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(internal.ActivityRegisterRequest)
	request.SetReplyType(internal.ActivityRegisterReply)

	return request
}

// GetName gets a ActivityRegisterRequest's Name field
// from its properties map.  Specifies the name of the activity to
// be registered.
//
// returns *string -> *string representing the name of the
// activity to be registered
func (request *ActivityRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets an ActivityRegisterRequest's Name field
// from its properties map.  Specifies the name of the activity to
// be registered.
//
// param value *string -> *string representing the name of the
// activity to be registered
func (request *ActivityRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetDisableAlreadyRegisteredCheck gets a ActivityRegisterRequest's DisableAlreadyRegisteredCheck value
// from its properties map
//
// returns bool
func (request *ActivityRegisterRequest) GetDisableAlreadyRegisteredCheck() bool {
	return request.GetBoolProperty("DisableAlreadyRegisteredCheck")
}

// SetDisableAlreadyRegisteredCheck sets a ActivityRegisterRequest's DisableAlreadyRegisteredCheck value
// in its properties map
//
// param value bool
func (request *ActivityRegisterRequest) SetDisableAlreadyRegisteredCheck(value bool) {
	request.SetBoolProperty("DisableAlreadyRegisteredCheck", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityRegisterRequest) Clone() IProxyMessage {
	activityRegisterRequest := NewActivityRegisterRequest()
	var messageClone IProxyMessage = activityRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityRegisterRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityRegisterRequest); ok {
		v.SetName(request.GetName())
		v.SetDisableAlreadyRegisteredCheck(request.GetDisableAlreadyRegisteredCheck())
	}
}
