package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// InitializeRequest is ProxyRequest of MessageType
	// InitializeRequest.
	//
	// A InitializeRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	InitializeRequest struct {
		*ProxyRequest
	}
)

// NewInitializeRequest is the default constructor for a InitializeRequest
//
// returns *InitializeRequest -> pointer to a newly initialized
// InitializeRequest in memory
func NewInitializeRequest() *InitializeRequest {
	request := new(InitializeRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.InitializeRequest)
	request.SetReplyType(messagetypes.InitializeReply)

	return request
}

// GetLibraryAddress gets the LibraryAddress property from an InitializeRequest
// in its properties map
//
// returns *string -> a pointer to a string in memory that holds the value
// of an InitializeRequest's LibraryAddress
func (request *InitializeRequest) GetLibraryAddress() *string {
	return request.GetStringProperty("LibraryAddress")
}

// SetLibraryAddress sets the LibraryAddress property in an INitializeRequest's
// properties map
//
// param value *string -> a pointer to a string that holds the LibraryAddress value
// to set in the request's properties map
func (request *InitializeRequest) SetLibraryAddress(value *string) {
	request.SetStringProperty("LibraryAddress", value)
}

// GetLibraryPort gets the LibraryPort property from an InitializeRequest
// in its properties map
//
// returns *string -> a pointer to a string in memory that holds the value
// of an InitializeRequest's LibraryPort
func (request *InitializeRequest) GetLibraryPort() int32 {
	return request.GetIntProperty("LibraryPort")
}

// SetLibraryPort sets the LibraryPort property in an INitializeRequest's
// properties map
//
// param value *string -> a pointer to a string that holds the LibraryPort value
// to set in the request's properties map
func (request *InitializeRequest) SetLibraryPort(value int32) {
	request.SetIntProperty("LibraryPort", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (request *InitializeRequest) Clone() IProxyMessage {
	initializeRequest := NewInitializeRequest()
	var messageClone IProxyMessage = initializeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (request *InitializeRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*InitializeRequest); ok {
		v.SetLibraryAddress(request.GetLibraryAddress())
		v.SetLibraryPort(request.GetLibraryPort())
	}
}
