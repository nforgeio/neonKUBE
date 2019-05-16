package endpoints

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"go.uber.org/zap"
)

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply messages.IProxyReply, typeCode messagetypes.MessageType) error {

	// error to catch any exceptions thrown in the
	// switch block
	var err error

	// handle the messages individually based on their message type
	switch typeCode {

	// InitializeReply
	case messagetypes.InitializeReply:
		if v, ok := reply.(*messages.InitializeReply); ok {
			err = handleInitializeReply(v)
		}

	// HeartbeatReply
	case messagetypes.HeartbeatReply:
		if v, ok := reply.(*messages.HeartbeatReply); ok {
			err = handleHeartbeatReply(v)
		}

	// CancelReply
	case messagetypes.CancelReply:
		if v, ok := reply.(*messages.CancelReply); ok {
			err = handleCancelReply(v)
		}

	// ConnectReply
	case messagetypes.ConnectReply:
		if v, ok := reply.(*messages.ConnectReply); ok {
			err = handleConnectReply(v)
		}

	// DomainDescribeReply
	case messagetypes.DomainDescribeReply:
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			err = handleDomainDescribeReply(v)
		}

	// DomainRegisterReply
	case messagetypes.DomainRegisterReply:
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			err = handleDomainRegisterReply(v)
		}

	// DomainUpdateReply
	case messagetypes.DomainUpdateReply:
		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			err = handleDomainUpdateReply(v)
		}

	// TerminateReply
	case messagetypes.TerminateReply:
		if v, ok := reply.(*messages.TerminateReply); ok {
			err = handleTerminateReply(v)
		}

	// WorkflowExecuteReply
	case messagetypes.WorkflowExecuteReply:
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			err = handleWorkflowExecuteReply(v)
		}

	// WorkflowInvokeReply
	case messagetypes.WorkflowInvokeReply:
		if v, ok := reply.(*messages.WorkflowInvokeReply); ok {
			err = handleWorkflowInvokeReply(v)
		}

	// WorkflowRegisterReply
	case messagetypes.WorkflowRegisterReply:
		if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
			err = handleWorkflowRegisterReply(v)
		}

	// NewWorkerReply
	case messagetypes.NewWorkerReply:
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			err = handleNewWorkerReply(v)
		}

	// StopWorkerReply
	case messagetypes.StopWorkerReply:
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			err = handleStopWorkerReply(v)
		}

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

func handleActivityReply(reply *messages.ActivityReply) error {
	err := fmt.Errorf("not implemented exception for message type ActivityReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ActivityReply", zap.Error(err))
	return err
}

func handleCancelReply(reply *messages.CancelReply) error {
	err := fmt.Errorf("not implemented exception for message type CancelReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling CancelReply", zap.Error(err))
	return err
}

func handleConnectReply(reply *messages.ConnectReply) error {
	err := fmt.Errorf("not implemented exception for message type ConnectReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling ConnectReply", zap.Error(err))
	return err
}

func handleDomainDescribeReply(reply *messages.DomainDescribeReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainDescribeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainDescribeReply", zap.Error(err))
	return err
}

func handleDomainRegisterReply(reply *messages.DomainRegisterReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainRegisterReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainRegisterReply", zap.Error(err))
	return err
}

func handleDomainUpdateReply(reply *messages.DomainUpdateReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainUpdateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainUpdateReply", zap.Error(err))
	return err
}

func handleHeartbeatReply(reply *messages.HeartbeatReply) error {
	err := fmt.Errorf("not implemented exception for message type HeartbeatReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling HeartbeatReply", zap.Error(err))
	return err
}

func handleInitializeReply(reply *messages.InitializeReply) error {
	err := fmt.Errorf("not implemented exception for message type InitializeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling InitializeReply", zap.Error(err))
	return err
}

func handleTerminateReply(reply *messages.TerminateReply) error {
	err := fmt.Errorf("not implemented exception for message type TerminateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling TerminateReply", zap.Error(err))
	return err
}

func handleWorkflowExecuteReply(reply *messages.WorkflowExecuteReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowExecuteReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowExecuteReply", zap.Error(err))
	return err
}

func handleWorkflowInvokeReply(reply *messages.WorkflowInvokeReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowInvokeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowInvokeReply", zap.Error(err))
	return err
}

func handleWorkflowRegisterReply(reply *messages.WorkflowRegisterReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowRegisterReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowRegisterReply", zap.Error(err))
	return err
}

func handleNewWorkerReply(reply *messages.NewWorkerReply) error {
	err := fmt.Errorf("not implemented exception for message type NewWorkerReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling NewWorkerReply", zap.Error(err))
	return err
}

func handleStopWorkerReply(reply *messages.StopWorkerReply) error {
	err := fmt.Errorf("not implemented exception for message type StopWorkerReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling StopWorkerReply", zap.Error(err))
	return err
}
