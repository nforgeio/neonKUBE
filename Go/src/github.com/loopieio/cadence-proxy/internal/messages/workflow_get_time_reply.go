package messages

import (
	"time"

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
