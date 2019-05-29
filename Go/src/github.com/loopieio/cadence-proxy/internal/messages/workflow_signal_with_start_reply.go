package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"go.uber.org/cadence/workflow"
)

type (

	// WorkflowSignalWithStartReply is a WorkflowReply of MessageType
	// WorkflowSignalWithStartReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalWithStartRequest
	WorkflowSignalWithStartReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalWithStartReply is the default constructor for
// a WorkflowSignalWithStartReply
//
// returns *WorkflowSignalWithStartReply -> a pointer to a newly initialized
// WorkflowSignalWithStartReply in memory
func NewWorkflowSignalWithStartReply() *WorkflowSignalWithStartReply {
	reply := new(WorkflowSignalWithStartReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalWithStartReply)

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

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalWithStartReply) Clone() IProxyMessage {
	workflowSignalWithStartReply := NewWorkflowSignalWithStartReply()
	var messageClone IProxyMessage = workflowSignalWithStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalWithStartReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowSignalWithStartReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowSignalWithStartReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowSignalWithStartReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowSignalWithStartReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowSignalWithStartReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowSignalWithStartReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowSignalWithStartReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowSignalWithStartReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowSignalWithStartReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowSignalWithStartReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowSignalWithStartReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
