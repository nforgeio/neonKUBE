//-----------------------------------------------------------------------------
// FILE:		activity_record_heartbeat_request.go
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

	// ActivityRecordHeartbeatRequest is an ActivityRequest of MessageType
	// ActivityRecordHeartbeatRequest.
	//
	// A ActivityRecordHeartbeatRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Records an activity heartbeat.
	ActivityRecordHeartbeatRequest struct {
		*ActivityRequest
	}
)

// NewActivityRecordHeartbeatRequest is the default constructor for a ActivityRecordHeartbeatRequest
//
// returns *ActivityRecordHeartbeatRequest -> a pointer to a newly initialized ActivityRecordHeartbeatRequest
// in memory
func NewActivityRecordHeartbeatRequest() *ActivityRecordHeartbeatRequest {
	request := new(ActivityRecordHeartbeatRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityRecordHeartbeatRequest)
	request.SetReplyType(messagetypes.ActivityRecordHeartbeatReply)

	return request
}

// GetTaskToken gets a ActivityRecordHeartbeatRequest's TaskToken field
// from its properties map. TaskToken is a []byte opaque activity task token.
//
// returns []byte -> []byte representing the opaque activity task token.
func (request *ActivityRecordHeartbeatRequest) GetTaskToken() []byte {
	return request.GetBytesProperty("TaskToken")
}

// SetTaskToken sets an ActivityRecordHeartbeatRequest's TaskToken field
// from its properties map.  TaskToken is a []byte opaque activity task token.
//
// param value []byte -> []byte representing the opaque activity task token.
func (request *ActivityRecordHeartbeatRequest) SetTaskToken(value []byte) {
	request.SetBytesProperty("TaskToken", value)
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityRecordHeartbeatRequest's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (request *ActivityRecordHeartbeatRequest) GetDetails() []byte {
	return request.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityRecordHeartbeatRequest's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityRecordHeartbeatRequest's properties map
func (request *ActivityRecordHeartbeatRequest) SetDetails(value []byte) {
	request.SetBytesProperty("Details", value)
}

// GetDomain gets a ActivityRecordHeartbeatRequest's Domain field
// from its properties map. Domain is the name of the cadence
// domain the activity is executing on.
//
// returns *string -> pointer to the string in memory
// of the cadence domain the activity is executing in.
func (request *ActivityRecordHeartbeatRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets an ActivityRecordHeartbeatRequest's Domain field
// in its properties map.  Domain is the name of the cadence
// domain the activity is executing on.
//
// param value *string -> pointer to the string in memory
// of the cadence domain the activity is executing in.
func (request *ActivityRecordHeartbeatRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetWorkflowID gets a ActivityRecordHeartbeatRequest's WorkflowID field
// from its properties map. WorkflowID is the ID of the cadence
// workflow executing the activity.
//
// returns *string -> pointer to the string in memory
// of the id of the workflow executing the activity.
func (request *ActivityRecordHeartbeatRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an ActivityRecordHeartbeatRequest's WorkflowID field
// from its properties map.  WorkflowID is the ID of the cadence
// workflow executing the activity.
//
// param value *string -> pointer to the string in memory
// of the id of the workflow executing the activity.
func (request *ActivityRecordHeartbeatRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a ActivityRecordHeartbeatRequest's RunID field
// from its properties map. RunID is the ID of the cadence
// workflow executing the activity.
//
// returns *string -> pointer to the string in memory
// of the run id of the workflow executing the activity.
func (request *ActivityRecordHeartbeatRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets an ActivityRecordHeartbeatRequest's RunID field
// from its properties map.  RunID is the ID of the cadence
// workflow executing the activity.
//
// param value *string -> pointer to the string in memory
// of the run id of the workflow executing the activity.
func (request *ActivityRecordHeartbeatRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetActivityID gets a ActivityRecordHeartbeatRequest's ActivityID field
// from its properties map. ActivityID is the ID of the executing
// cadence activity.
//
// returns *string -> pointer to the string in memory
// of the id of the executing activity.
func (request *ActivityRecordHeartbeatRequest) GetActivityID() *string {
	return request.GetStringProperty("ActivityId")
}

// SetActivityID sets an ActivityRecordHeartbeatRequest's ActivityID field
// from its properties map.  ActivityID is the ID of the executing
// cadence activity.
//
// param value *string -> pointer to the string in memory
// of the id of the executing activity.
func (request *ActivityRecordHeartbeatRequest) SetActivityID(value *string) {
	request.SetStringProperty("ActivityId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityRecordHeartbeatRequest) Clone() IProxyMessage {
	activityRecordHeartbeatRequest := NewActivityRecordHeartbeatRequest()
	var messageClone IProxyMessage = activityRecordHeartbeatRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityRecordHeartbeatRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityRecordHeartbeatRequest); ok {
		v.SetTaskToken(request.GetTaskToken())
		v.SetDetails(request.GetDetails())
		v.SetDomain(request.GetDomain())
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetActivityID(request.GetActivityID())
	}
}
