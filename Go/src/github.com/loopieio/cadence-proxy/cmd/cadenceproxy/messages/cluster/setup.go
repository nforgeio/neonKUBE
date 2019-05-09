package cluster

import (
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

// FillMessageTypeStructMap fills the MessageTypeStructMap initialized cluster messages
// mapped to their message types
func FillMessageTypeStructMap() {

	// InitCancel is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys CancelRequest and CancelReply.
	// The values are new instances of a CancelRequest and CancelReply
	key := int(messages.CancelRequest)
	base.MessageTypeStructMap[key] = NewCancelRequest()

	key = int(messages.CancelReply)
	base.MessageTypeStructMap[key] = NewCancelReply()

	// InitConnect is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys ConnectRequest and ConnectReply.
	// The values are new instances of a ConnectRequest and ConnectReply
	key = int(messages.ConnectRequest)
	base.MessageTypeStructMap[key] = NewConnectRequest()

	key = int(messages.ConnectReply)
	base.MessageTypeStructMap[key] = NewConnectReply()

	// InitDomainDescribe is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys DomainDescribeRequest and DomainDescribeReply.
	// The values are new instances of a DomainDescribeRequest and DomainDescribeReply
	key = int(messages.DomainDescribeRequest)
	base.MessageTypeStructMap[key] = NewDomainDescribeRequest()

	key = int(messages.DomainDescribeReply)
	base.MessageTypeStructMap[key] = NewDomainDescribeReply()

	// InitDomainRegister is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys DomainRegisterRequest and DomainRegisterReply.
	// The values are new instances of a DomainRegisterRequest and DomainRegisterReply
	key = int(messages.DomainRegisterRequest)
	base.MessageTypeStructMap[key] = NewDomainRegisterRequest()

	key = int(messages.DomainRegisterReply)
	base.MessageTypeStructMap[key] = NewDomainRegisterReply()

	// InitDomainUpdate is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys DomainUpdateRequest and DomainUpdateReply.
	// The values are new instances of a DomainUpdateRequest and DomainUpdateReply
	key = int(messages.DomainUpdateRequest)
	base.MessageTypeStructMap[key] = NewDomainUpdateRequest()

	key = int(messages.DomainUpdateReply)
	base.MessageTypeStructMap[key] = NewDomainUpdateReply()

	// InitHeartbeat is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys HeartbeatRequest and HeartbeatReply.
	// The values are new instances of a HeartbeatRequest and HeartbeatReply
	key = int(messages.HeartbeatRequest)
	base.MessageTypeStructMap[key] = NewHeartbeatRequest()

	key = int(messages.HeartbeatReply)
	base.MessageTypeStructMap[key] = NewHeartbeatReply()

	// InitInitialize is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys InitializeRequest and InitializeReply.
	// The values are new instances of a InitializeRequest and InitializeReply
	key = int(messages.InitializeRequest)
	base.MessageTypeStructMap[key] = NewInitializeRequest()

	key = int(messages.InitializeReply)
	base.MessageTypeStructMap[key] = NewInitializeReply()

	// InitTerminate is a method that adds a key/value entry into the
	// MessageTypeStructMap at keys TerminateRequest and TerminateReply.
	// The values are new instances of a TerminateRequest and TerminateReply
	key = int(messages.TerminateRequest)
	base.MessageTypeStructMap[key] = NewTerminateRequest()

	key = int(messages.TerminateReply)
	base.MessageTypeStructMap[key] = NewTerminateReply()

}
