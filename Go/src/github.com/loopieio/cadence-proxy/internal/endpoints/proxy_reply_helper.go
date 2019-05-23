package endpoints

import (
	"fmt"
	"os"

	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceworkflows"

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

	// WorkflowCancelReply
	case messagetypes.WorkflowCancelReply:
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			err = handleWorkflowCancelReply(v)
		}

	// WorkflowSignalReply
	case messagetypes.WorkflowSignalReply:
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			err = handleWorkflowSignalReply(v)
		}

	// WorkflowSignalWithStartReply
	case messagetypes.WorkflowSignalWithStartReply:
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			err = handleWorkflowSignalWithStartReply(v)
		}

	// WorkflowQueryReply
	case messagetypes.WorkflowQueryReply:
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			err = handleWorkflowQueryReply(v)
		}

	// WorkflowSetCacheSizeReply
	case messagetypes.WorkflowSetCacheSizeReply:
		if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
			err = handleWorkflowSetCacheSizeReply(v)
		}

	// WorkflowMutableReply
	case messagetypes.WorkflowMutableReply:
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			err = handleWorkflowMutableReply(v)
		}

	// WorkflowMutableInvokeReply
	case messagetypes.WorkflowMutableInvokeReply:
		if v, ok := reply.(*messages.WorkflowMutableInvokeReply); ok {
			err = handleWorkflowMutableInvokeReply(v)
		}

	// PingReply
	case messagetypes.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			err = handlePingReply(v)
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

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowInvokeReply Recieved", zap.Int("ProccessId", os.Getpid()))

	// WorkflowExecutionContext at the specified WorflowContextID
	workflowExecutionContextID := reply.GetWorkflowContextID()
	if wectx := cadenceworkflows.WorkflowExecutionContexts.Get(workflowExecutionContextID); wectx == nil {
		return entityNotExistError
	}

	// get the Operation corresponding the the reply
	requestID := reply.GetRequestID()
	op := Operations.Get(requestID)
	err := op.SetReply(reply, reply.GetResult())
	if err != nil {
		return err
	}

	// remove the WorkflowExecutionContext from the map
	// and remove the Operation from the map
	_ = cadenceworkflows.WorkflowExecutionContexts.Remove(workflowExecutionContextID)
	_ = Operations.Remove(requestID)

	return nil
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

func handleWorkflowCancelReply(reply *messages.WorkflowCancelReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowCancelReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowCancelReply", zap.Error(err))
	return err
}

func handleWorkflowSignalReply(reply *messages.WorkflowSignalReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowSignalReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowSignalReply", zap.Error(err))
	return err
}

func handleWorkflowSignalWithStartReply(reply *messages.WorkflowSignalWithStartReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowSignalWithStartReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowSignalWithStartReply", zap.Error(err))
	return err
}

func handleWorkflowQueryReply(reply *messages.WorkflowQueryReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowQueryReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowQueryReply", zap.Error(err))
	return err
}

func handleWorkflowSetCacheSizeReply(reply *messages.WorkflowSetCacheSizeReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowSetCacheSizeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowSetCacheSizeReply", zap.Error(err))
	return err
}

func handleWorkflowMutableReply(reply *messages.WorkflowMutableReply) error {
	err := fmt.Errorf("not implemented exception for message type WorkflowMutableReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling WorkflowMutableReply", zap.Error(err))
	return err
}

func handleWorkflowMutableInvokeReply(reply *messages.WorkflowMutableInvokeReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowMutableInvokeReply Recieved", zap.Int("ProccessId", os.Getpid()))

	// WorkflowExecutionContext at the specified WorflowContextID
	workflowExecutionContextID := reply.GetWorkflowContextID()
	if wectx := cadenceworkflows.WorkflowExecutionContexts.Get(workflowExecutionContextID); wectx == nil {
		return entityNotExistError
	}

	// get the Operation corresponding the the reply
	requestID := reply.GetRequestID()
	op := Operations.Get(requestID)
	err := op.SetReply(reply, reply.GetResult())
	if err != nil {
		return err
	}

	// remove the WorkflowExecutionContext from the map
	// and remove the Operation from the map
	_ = cadenceworkflows.WorkflowExecutionContexts.Remove(workflowExecutionContextID)
	_ = Operations.Remove(requestID)

	return nil
}

func handlePingReply(reply *messages.PingReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowInvokeReply Recieved", zap.Int("ProccessId", os.Getpid()))
	return nil
}
