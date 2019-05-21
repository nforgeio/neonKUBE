package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	"go.uber.org/cadence/workflow"
)

type (

	// WorkflowSignalWithStartReply is a WorkflowContextReply of MessageType
	// WorkflowSignalWithStartReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowSignalWithStartRequest
	WorkflowSignalWithStartReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowSignalWithStartReply is the default constructor for
// a WorkflowSignalWithStartReply
//
// returns *WorkflowSignalWithStartReply -> a pointer to a newly initialized
// WorkflowSignalWithStartReply in memory
func NewWorkflowSignalWithStartReply() *WorkflowSignalWithStartReply {
	reply := new(WorkflowSignalWithStartReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowSignalWithStartReply

	return reply
}

// GetExecution gets the cadence workflow.Execution or nil
// from a WorkflowSignalWithStartReply's properties map.
//
// returns *workflow.Execution -> pointer to a cadence workflow.Execution
// struct housing the result of a workflow execution
func (reply *WorkflowSignalWithStartReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := reply.GetJSONProperty("Execution", exe)
	if err != nil {
		return nil
	}

	return exe
}

// SetExecution sets the cadence workflow.Execution or nil
// in a WorkflowSignalWithStartReply's properties map.
//
// param value *workflow.Execution -> pointer to a cadence workflow execution
// struct housing the result of a started workflow, to be set in the
// WorkflowSignalWithStartReply's properties map
func (reply *WorkflowSignalWithStartReply) SetExecution(value *workflow.Execution) {
	reply.SetJSONProperty("Execution", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextReply.Clone()
func (reply *WorkflowSignalWithStartReply) Clone() IProxyMessage {
	workflowSignalWithStartReply := NewWorkflowSignalWithStartReply()
	var messageClone IProxyMessage = workflowSignalWithStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextReply.CopyTo()
func (reply *WorkflowSignalWithStartReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
	if v, ok := target.(*WorkflowSignalWithStartReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowSignalWithStartReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowSignalWithStartReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowSignalWithStartReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowSignalWithStartReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowSignalWithStartReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowSignalWithStartReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowSignalWithStartReply) GetWorkflowContextID() int64 {
	return reply.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowSignalWithStartReply) SetWorkflowContextID(value int64) {
	reply.WorkflowContextReply.SetWorkflowContextID(value)
}
