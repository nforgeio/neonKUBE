package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSleepRequest is WorkflowRequest of MessageType
	// WorkflowSleepRequest.
	//
	// A WorkflowSleepRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Commands the workflow to sleep for a period of time.
	WorkflowSleepRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSleepRequest is the default constructor for a WorkflowSleepRequest
//
// returns *WorkflowSleepRequest -> a reference to a newly initialized
// WorkflowSleepRequest in memory
func NewWorkflowSleepRequest() *WorkflowSleepRequest {
	request := new(WorkflowSleepRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSleepRequest)
	request.SetReplyType(messagetypes.WorkflowSleepReply)

	return request
}

// GetDuration gets the Duration property from the WorkflowSleepRequest's
// properties map. Duration specifies the time to sleep.
//
// returns time.Duration -> the value of the Duration property from
// the WorkflowSleepRequest's properties map.
func (request *WorkflowSleepRequest) GetDuration() time.Duration {
	return request.GetTimeSpanProperty("Duration")
}

// SetDuration sets the Duration property in the WorkflowSleepRequest's
// properties map. Duration specifies the time to sleep.
//
// param value time.Duration -> the time.Duration to be set in the
// WorkflowSleepRequest's properties map.
func (request *WorkflowSleepRequest) SetDuration(value time.Duration) {
	request.SetTimeSpanProperty("Duration", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSleepRequest) Clone() IProxyMessage {
	workflowSleepRequest := NewWorkflowSleepRequest()
	var messageClone IProxyMessage = workflowSleepRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSleepRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSleepRequest); ok {
		v.SetDuration(request.GetDuration())
	}
}
