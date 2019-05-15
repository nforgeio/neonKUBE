package endpoints

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/internal/messages"
	"github.com/loopieio/cadence-proxy/internal/messages/types"
	"go.uber.org/zap"
)

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply types.IProxyReply, typeCode messages.MessageType) error {

	// error to catch any exceptions thrown in the
	// switch block
	var err error

	// handle the messages individually based on their message type
	switch typeCode {

	// InitializeReply
	case messages.InitializeReply:
		err = handleInitializeReply(reply)

	// HeartbeatReply
	case messages.HeartbeatReply:
		err = handleHeartbeatReply(reply)

	// CancelReply
	case messages.CancelReply:
		err = handleCancelReply(reply)

	// ConnectReply
	case messages.ConnectReply:
		err = handleConnectReply(reply)

	// DomainDescribeReply
	case messages.DomainDescribeReply:
		err = handleDomainDescribeReply(reply)

	// DomainRegisterReply
	case messages.DomainRegisterReply:
		err = handleDomainRegisterReply(reply)

	// DomainUpdateReply
	case messages.DomainUpdateReply:
		err = handleDomainUpdateReply(reply)

	// TerminateReply
	case messages.TerminateReply:
		err = handleTerminateReply(reply)

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", typeCode)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any exceptions returned in
	// the switch block
	if err != nil {
		return err
	}

	return nil
}

func handleActivityReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type ActivityReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityReply", zap.Error(err))
	return err
}

func handleCancelReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type CancelReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling CancelReply", zap.Error(err))
	return err
}

func handleConnectReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type ConnectReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ConnectReply", zap.Error(err))
	return err
}

func handleDomainDescribeReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainDescribeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainDescribeReply", zap.Error(err))
	return err
}

func handleDomainRegisterReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainRegisterReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainRegisterReply", zap.Error(err))
	return err
}

func handleDomainUpdateReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainUpdateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainUpdateReply", zap.Error(err))
	return err
}

func handleHeartbeatReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type HeartbeatReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling HeartbeatReply", zap.Error(err))
	return err
}

func handleInitializeReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type InitializeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling InitializeReply", zap.Error(err))
	return err
}

func handleTerminateReply(reply types.IProxyReply) error {
	err := fmt.Errorf("not implemented exception for message type TerminateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling TerminateReply", zap.Error(err))
	return err
}
