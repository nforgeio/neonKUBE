package messages

import (
	"go.uber.org/cadence/workflow"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityExecuteLocalRequest is an ActivityRequest of MessageType
	// ActivityExecuteLocalRequest.
	//
	// A ActivityExecuteLocalRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Starts a local workflow activity.
	ActivityExecuteLocalRequest struct {
		*ActivityRequest
	}
)

// NewActivityExecuteLocalRequest is the default constructor for a ActivityExecuteLocalRequest
//
// returns *ActivityExecuteLocalRequest -> a pointer to a newly initialized ActivityExecuteLocalRequest
// in memory
func NewActivityExecuteLocalRequest() *ActivityExecuteLocalRequest {
	request := new(ActivityExecuteLocalRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityExecuteLocalRequest)
	request.SetReplyType(messagetypes.ActivityExecuteLocalReply)

	return request
}

// GetActivityTypeID gets a ActivityExecuteLocalRequest's ActivityTypeID field
// from its properties map.  Identifies the .NET type that
// implements the local activity.
//
// returns int64 -> int64 representing the ActivityTypeID of the
// activity to be executed
func (request *ActivityExecuteLocalRequest) GetActivityTypeID() int64 {
	return request.GetLongProperty("ActivityTypeId")
}

// SetActivityTypeID sets an ActivityExecuteLocalRequest's ActivityTypeID field
// from its properties map.  Identifies the .NET type that
// implements the local activity.
//
// param value int64 -> int64 representing the ActivityTypeID of the
// activity to be executed
func (request *ActivityExecuteLocalRequest) SetActivityTypeID(value int64) {
	request.SetLongProperty("ActivityTypeId", value)
}

// GetArgs gets a ActivityExecuteLocalRequest's Args field
// from its properties map.  Optionally specifies the
// arguments to be passed to the activity encoded as a byte array.
//
// returns []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityExecuteLocalRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityExecuteLocalRequest's Args field
// from its properties map.  Optionally specifies the
// arguments to be passed to the activity encoded as a byte array.
//
// param value []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityExecuteLocalRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a ActivityExecutionRequest's local
// activity start options.
//
// returns client.StartActivityOptions -> a cadence client struct that contains the
// options for executing a workflow activity
func (request *ActivityExecuteLocalRequest) GetOptions() *workflow.ActivityOptions {
	opts := new(workflow.ActivityOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a ActivityExecutionRequest's local
// activity start options.
//
// param value client.StartActivityOptions -> a cadence client struct that contains the
// options for executing a workflow activity to be set in the ActivityExecutionRequest's
// properties map
func (request *ActivityExecuteLocalRequest) SetOptions(value *workflow.ActivityOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityExecuteLocalRequest) Clone() IProxyMessage {
	activityExecuteLocalRequest := NewActivityExecuteLocalRequest()
	var messageClone IProxyMessage = activityExecuteLocalRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityExecuteLocalRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityExecuteLocalRequest); ok {
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
		v.SetActivityTypeID(request.GetActivityTypeID())
	}
}
