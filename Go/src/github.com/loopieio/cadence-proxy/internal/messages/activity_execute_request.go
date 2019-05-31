package messages

import (
	"go.uber.org/cadence/workflow"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityExecuteRequest is an ActivityRequest of MessageType
	// ActivityExecuteRequest.
	//
	// A ActivityExecuteRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Starts a workflow activity.
	ActivityExecuteRequest struct {
		*ActivityRequest
	}
)

// NewActivityExecuteRequest is the default constructor for a ActivityExecuteRequest
//
// returns *ActivityExecuteRequest -> a pointer to a newly initialized ActivityExecuteRequest
// in memory
func NewActivityExecuteRequest() *ActivityExecuteRequest {
	request := new(ActivityExecuteRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityExecuteRequest)
	request.SetReplyType(messagetypes.ActivityExecuteReply)

	return request
}

// GetName gets a ActivityExecuteRequest's Name field
// from its properties map.  Specifies the name of the activity to
// be executed.
//
// returns *string -> *string representing the name of the
// activity to be executed
func (request *ActivityExecuteRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets an ActivityExecuteRequest's Name field
// from its properties map.  Specifies the name of the activity to
// be executed.
//
// param value *string -> *string representing the name of the
// activity to be executed
func (request *ActivityExecuteRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetArgs gets a ActivityExecuteRequest's Args field
// from its properties map.  Args is a []byte that hold the arguments
// for executing a specific workflow activity
//
// returns []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityExecuteRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityExecuteRequest's Args field
// from its properties map.  Args is a []byte that hold the arguments
// for executing a specific workflow activity
//
// param value []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityExecuteRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a ActivityExecutionRequest's start options
// used to execute a cadence workflow activity via the cadence workflow client
//
// returns client.StartActivityOptions -> a cadence client struct that contains the
// options for executing a workflow activity
func (request *ActivityExecuteRequest) GetOptions() *workflow.ActivityOptions {
	opts := new(workflow.ActivityOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a ActivityExecutionRequest's start options
// used to execute a cadence workflow activity via the cadence workflow client
//
// param value client.StartActivityOptions -> a cadence client struct that contains the
// options for executing a workflow activity to be set in the ActivityExecutionRequest's
// properties map
func (request *ActivityExecuteRequest) SetOptions(value *workflow.ActivityOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityExecuteRequest) Clone() IProxyMessage {
	activityExecuteRequest := NewActivityExecuteRequest()
	var messageClone IProxyMessage = activityExecuteRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityExecuteRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityExecuteRequest); ok {
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
		v.SetName(request.GetName())
	}
}
