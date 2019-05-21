package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetCacheSizeRequest is WorkflowContextRequest of MessageType
	// WorkflowSetCacheSizeRequest.
	//
	// A WorkflowSetCacheSizeRequest contains a reference to a
	// WorkflowContextRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowContextRequest
	//
	// A WorkflowSetCacheSizeRequest sets the maximum number of bytes the client will use
	/// to cache the history of a sticky workflow on a workflow worker as a performance
	/// optimization.  When this is exceeded for a workflow, its full history will
	/// need to be retrieved from the Cadence cluster the next time the workflow
	/// instance is assigned to a worker.
	WorkflowSetCacheSizeRequest struct {
		*WorkflowContextRequest
	}
)

// NewWorkflowSetCacheSizeRequest is the default constructor for a WorkflowSetCacheSizeRequest
//
// returns *WorkflowSetCacheSizeRequest -> a reference to a newly initialized
// WorkflowSetCacheSizeRequest in memory
func NewWorkflowSetCacheSizeRequest() *WorkflowSetCacheSizeRequest {
	request := new(WorkflowSetCacheSizeRequest)
	request.WorkflowContextRequest = NewWorkflowContextRequest()
	request.Type = messagetypes.WorkflowSetCacheSizeRequest
	request.SetReplyType(messagetypes.WorkflowSetCacheSizeReply)

	return request
}

// GetSize gets a WorkflowSetCacheSizeRequest's Size value
// from its properties map.  Specifies the maximum number of bytes used for
// caching sticky workflows.
//
// returns int -> int specifying the maximum number of bytes used for caching
// sticky workflows.cache Size
func (request *WorkflowSetCacheSizeRequest) GetSize() int {
	return int(request.GetIntProperty("Size"))
}

// SetSize sets a WorkflowSetCacheSizeRequest's Size value
// in its properties map
//
// param value int -> int specifying the maximum number of bytes used for caching
// sticky workflows.cache Size
func (request *WorkflowSetCacheSizeRequest) SetSize(value int) {
	request.SetIntProperty("Size", int32(value))
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextRequest.Clone()
func (request *WorkflowSetCacheSizeRequest) Clone() IProxyMessage {
	workflowSetCacheSizeRequest := NewWorkflowSetCacheSizeRequest()
	var messageClone IProxyMessage = workflowSetCacheSizeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextRequest.CopyTo()
func (request *WorkflowSetCacheSizeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowContextRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSetCacheSizeRequest); ok {
		v.SetSize(request.GetSize())
	}
}

// SetProxyMessage inherits docs from WorkflowContextRequest.SetProxyMessage()
func (request *WorkflowSetCacheSizeRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowContextRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextRequest.GetProxyMessage()
func (request *WorkflowSetCacheSizeRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowContextRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextRequest.GetRequestID()
func (request *WorkflowSetCacheSizeRequest) GetRequestID() int64 {
	return request.WorkflowContextRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextRequest.SetRequestID()
func (request *WorkflowSetCacheSizeRequest) SetRequestID(value int64) {
	request.WorkflowContextRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowContextRequest.GetReplyType()
func (request *WorkflowSetCacheSizeRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowContextRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowContextRequest.SetReplyType()
func (request *WorkflowSetCacheSizeRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowContextRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowContextRequest.GetTimeout()
func (request *WorkflowSetCacheSizeRequest) GetTimeout() time.Duration {
	return request.WorkflowContextRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowContextRequest.SetTimeout()
func (request *WorkflowSetCacheSizeRequest) SetTimeout(value time.Duration) {
	request.WorkflowContextRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextRequest interface methods for implementing the IWorkflowContextRequest interface

// GetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (reply *WorkflowSetCacheSizeRequest) GetWorkflowContextID() int64 {
	return reply.WorkflowContextRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (reply *WorkflowSetCacheSizeRequest) SetWorkflowContextID(value int64) {
	reply.WorkflowContextRequest.SetWorkflowContextID(value)
}
