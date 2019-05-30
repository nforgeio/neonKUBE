package messages

import (
	"time"

	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetTimeReply is a WorkflowReply of MessageType
	// WorkflowGetTimeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowGetTimeRequest
	WorkflowGetTimeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetTimeReply is the default constructor for
// a WorkflowGetTimeReply
//
// returns *WorkflowGetTimeReply -> a pointer to a newly initialized
// WorkflowGetTimeReply in memory
func NewWorkflowGetTimeReply() *WorkflowGetTimeReply {
	reply := new(WorkflowGetTimeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowGetTimeReply)

	return reply
}

// GetTime gets the Time property from the WorkflowGetTimeReply's
// properties map. Time is the current workflow time expressed as
// 100 nanosecond ticks since 01/01/0001 00:00.
//
// returns time.Time -> the value of the Time property from
// the WorkflowGetTimeReply's properties map.
func (reply *WorkflowGetTimeReply) GetTime() time.Time {
	return reply.GetDateTimeProperty("Time")
}

// SetTime sets the Time property in the WorkflowGetTimeReply's
// properties map. Time is the current workflow time expressed as
// 100 nanosecond ticks since 01/01/0001 00:00.
//
// param value time.Time -> the Time to be set in the
// WorkflowGetTimeReply's properties map.
func (reply *WorkflowGetTimeReply) SetTime(value time.Time) {
	reply.SetDateTimeProperty("Time", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowGetTimeReply) Clone() IProxyMessage {
	workflowGetTimeReply := NewWorkflowGetTimeReply()
	var messageClone IProxyMessage = workflowGetTimeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowGetTimeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetTimeReply); ok {
		v.SetTime(reply.GetTime())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowGetTimeReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowGetTimeReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowGetTimeReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowGetTimeReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowGetTimeReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowGetTimeReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowGetTimeReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowGetTimeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowGetTimeReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowGetTimeReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
