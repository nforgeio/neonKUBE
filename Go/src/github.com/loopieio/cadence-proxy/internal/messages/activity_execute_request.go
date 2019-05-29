package messages

import (
	"time"

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
	}
}

// SetProxyMessage inherits docs from ActivityRequest.SetProxyMessage()
func (request *ActivityExecuteRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityRequest.GetProxyMessage()
func (request *ActivityExecuteRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityRequest.GetRequestID()
func (request *ActivityExecuteRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ActivityRequest.SetRequestID()
func (request *ActivityExecuteRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// GetType inherits docs from ActivityRequest.GetType()
func (request *ActivityExecuteRequest) GetType() messagetypes.MessageType {
	return request.ActivityRequest.GetType()
}

// SetType inherits docs from ActivityRequest.SetType()
func (request *ActivityExecuteRequest) SetType(value messagetypes.MessageType) {
	request.ActivityRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityExecuteRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityExecuteRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityExecuteRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityExecuteRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID inherits docs from ActivityRequest.GetContextID()
func (request *ActivityExecuteRequest) GetContextID() int64 {
	return request.ActivityRequest.GetContextID()
}

// SetContextID inherits docs from ActivityRequest.SetContextID()
func (request *ActivityExecuteRequest) SetContextID(value int64) {
	request.ActivityRequest.SetContextID(value)
}
