//-----------------------------------------------------------------------------
// FILE:		activity_complete_request.go
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
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
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
// in its properties map.  Result is the []byte result to set in the activity
// complete call.
//
// param value []byte -> []byte value of the result to set in activity complete
func (request *ActivityCompleteRequest) SetResult(value []byte) {
	request.SetBytesProperty("Result", value)
}

// GetError gets a ActivityCompleteRequest's Error field
// from its properties map. Error is the *cadenceerrors.CadenceError to set in the activity
// complete call.
//
// returns *cadenceerrors.CadenceError -> *cadenceerrors.CadenceError to set in activity complete
func (request *ActivityCompleteRequest) GetError() *cadenceerrors.CadenceError {
	cadenceError := cadenceerrors.NewCadenceErrorEmpty()
	err := request.GetJSONProperty("Error", cadenceError)
	if err != nil {
		return nil
	}

	return cadenceError
}

// SetError sets an ActivityCompleteRequest's Error field
// from its properties map.  Error is the *cadenceerrors.CadenceError to set in the activity
// complete call.
//
// param value *cadenceerrors.CadenceError -> *cadenceerrors.CadenceError value to set in activity complete
func (request *ActivityCompleteRequest) SetError(value *cadenceerrors.CadenceError) {
	request.SetJSONProperty("Error", value)
}

// GetDomain gets a ActivityCompleteRequest's Domain field
// from its properties map. Domain is the name of the cadence
// domain the activity is executing on.
//
// returns *string -> pointer to the string in memory
// of the cadence domain the activity is executing in.
func (request *ActivityCompleteRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets an ActivityCompleteRequest's Domain field
// in its properties map.  Domain is the name of the cadence
// domain the activity is executing on.
//
// param value *string -> pointer to the string in memory
// of the cadence domain the activity is executing in.
func (request *ActivityCompleteRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetWorkflowID gets a ActivityCompleteRequest's WorkflowID field
// from its properties map. WorkflowID is the ID of the cadence
// workflow executing the activity.
//
// returns *string -> pointer to the string in memory
// of the id of the workflow executing the activity.
func (request *ActivityCompleteRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an ActivityCompleteRequest's WorkflowID field
// from its properties map.  WorkflowID is the ID of the cadence
// workflow executing the activity.
//
// param value *string -> pointer to the string in memory
// of the id of the workflow executing the activity.
func (request *ActivityCompleteRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a ActivityCompleteRequest's RunID field
// from its properties map. RunID is the ID of the cadence
// workflow executing the activity.
//
// returns *string -> pointer to the string in memory
// of the run id of the workflow executing the activity.
func (request *ActivityCompleteRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets an ActivityCompleteRequest's RunID field
// from its properties map.  RunID is the ID of the cadence
// workflow executing the activity.
//
// param value *string -> pointer to the string in memory
// of the run id of the workflow executing the activity.
func (request *ActivityCompleteRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetActivityID gets a ActivityCompleteRequest's ActivityID field
// from its properties map. ActivityID is the ID of the executing
// cadence activity.
//
// returns *string -> pointer to the string in memory
// of the id of the executing activity.
func (request *ActivityCompleteRequest) GetActivityID() *string {
	return request.GetStringProperty("ActivityId")
}

// SetActivityID sets an ActivityCompleteRequest's ActivityID field
// from its properties map.  ActivityID is the ID of the executing
// cadence activity.
//
// param value *string -> pointer to the string in memory
// of the id of the executing activity.
func (request *ActivityCompleteRequest) SetActivityID(value *string) {
	request.SetStringProperty("ActivityId", value)
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
		v.SetError(request.GetError())
		v.SetDomain(request.GetDomain())
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetActivityID(request.GetActivityID())
	}
}
