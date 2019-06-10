//-----------------------------------------------------------------------------
// FILE:		activity_complete_request.go
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
	"errors"

	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ActivityCompleteRequest is an ActivityRequest of MessageType
	// ActivityCompleteRequest.
	//
	// A ActivityCompleteRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Sent to a worker, instructing it to begin executing
	// a workflow activity.
	ActivityCompleteRequest struct {
		*ActivityRequest
	}
)

// NewActivityCompleteRequest is the default constructor for a ActivityCompleteRequest
//
// returns *ActivityCompleteRequest -> a pointer to a newly initialized ActivityCompleteRequest
// in memory
func NewActivityCompleteRequest() *ActivityCompleteRequest {
	request := new(ActivityCompleteRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityCompleteRequest)
	request.SetReplyType(messagetypes.ActivityCompleteReply)

	return request
}

// GetTaskToken gets a ActivityCompleteRequest's TaskToken field
// from its properties map. TaskToken is a []byte opaque activity task token.
//
// returns []byte -> []byte representing the opaque activity task token.
func (request *ActivityCompleteRequest) GetTaskToken() []byte {
	return request.GetBytesProperty("TaskToken")
}

// SetTaskToken sets an ActivityCompleteRequest's TaskToken field
// from its properties map.  TaskToken is a []byte opaque activity task token.
//
// param value []byte -> []byte representing the opaque activity task token.
func (request *ActivityCompleteRequest) SetTaskToken(value []byte) {
	request.SetBytesProperty("TaskToken", value)
}

// GetResult gets a ActivityCompleteRequest's Result field
// from its properties map. Result is the []byte result to set in the activity
// complete call.
//
// returns []byte -> []byte value of the result to set in activity complete
func (request *ActivityCompleteRequest) GetResult() []byte {
	return request.GetBytesProperty("Result")
}

// SetResult sets an ActivityCompleteRequest's Result field
// from its properties map.  Result is the []byte result to set in the activity
// complete call.
//
// param value []byte -> []byte value of the result to set in activity complete
func (request *ActivityCompleteRequest) SetResult(value []byte) {
	request.SetBytesProperty("Result", value)
}

// GetCompleteError gets a ActivityCompleteRequest's CompleteError field
// from its properties map. CompleteError is the error to set in the activity
// complete call.
//
// returns error -> error to set in activity complete
func (request *ActivityCompleteRequest) GetCompleteError() error {
	errStr := request.GetStringProperty("CompleteError")
	return errors.New(*errStr)
}

// SetCompleteError sets an ActivityCompleteRequest's CompleteError field
// from its properties map.  CompleteError is the error to set in the activity
// complete call.
//
// param value error -> error value to set in activity complete
func (request *ActivityCompleteRequest) SetCompleteError(value error) {
	errStr := value.Error()
	request.SetStringProperty("CompleteError", &errStr)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityCompleteRequest) Clone() IProxyMessage {
	activityCompleteRequest := NewActivityCompleteRequest()
	var messageClone IProxyMessage = activityCompleteRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityCompleteRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityCompleteRequest); ok {
		v.SetTaskToken(request.GetTaskToken())
		v.SetResult(request.GetResult())
		v.SetCompleteError(request.GetCompleteError())
	}
}
