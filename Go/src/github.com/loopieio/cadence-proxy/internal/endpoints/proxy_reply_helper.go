package endpoints

import (
	"fmt"
	"os"
	"time"

	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"

	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply messages.IProxyReply) error {

	// error to catch any exceptions thrown in the
	// switch block
	var err error

	// handle the messages individually based on their message type
	switch reply.GetType() {

	// -------------------------------------------------------------------------
	// client message types

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

	// PingReply
	case messagetypes.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			err = handlePingReply(v)
		}

	// -------------------------------------------------------------------------
	// Workflow message types

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

	// WorkflowSignalReceivedReply
	case messagetypes.WorkflowSignalReceivedReply:
		if v, ok := reply.(*messages.WorkflowSignalReceivedReply); ok {
			err = handleWorkflowSignalReceivedReply(v)
		}

	// -------------------------------------------------------------------------
	// Activity message types

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", reply.GetType())

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

func handleCancelReply(reply *messages.CancelReply) error {
	err := fmt.Errorf("not implemented exception for message type CancelReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling CancelReply", zap.Error(err))
	return err
}

func handleConnectReply(reply *messages.ConnectReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ConnectReply Recieved", zap.Int("ProccessId", os.Getpid()))

	return nil
}

func handleDomainDescribeReply(reply *messages.DomainDescribeReply) error {
	err := fmt.Errorf("not implemented exception for message type DomainDescribeReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling DomainDescribeReply", zap.Error(err))
	return err
}

func handleDomainRegisterReply(reply *messages.DomainRegisterReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterReply Recieved", zap.Int("ProccessId", os.Getpid()))

	return nil
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

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowRegisterReply Recieved", zap.Int("ProccessId", os.Getpid()))

	return nil
}

func handleWorkflowInvokeReply(reply *messages.WorkflowInvokeReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowInvokeReply Recieved", zap.Int("ProccessId", os.Getpid()))

	// WorkflowContext at the specified WorflowContextID
	contextID := reply.GetContextID()
	wectx := cadenceworkflows.WorkflowContexts.Get(contextID)
	if wectx == nil {
		return entityNotExistError
	}

	// TODO: JACK -- FILL OUT THE REST OF THE OPTIONS
	// THERE IS NO WITH RETRY POLICY
	// check for ContinueAsNew
	if reply.GetContinueAsNew() {
		continueContext := wectx.GetContext()

		if reply.GetContinueAsNewDomain() != nil {
			continueContext = workflow.WithWorkflowDomain(continueContext, *reply.GetContinueAsNewDomain())
		}

		if reply.GetContinueAsNewTaskList() != nil {
			continueContext = workflow.WithTaskList(continueContext, *reply.GetContinueAsNewTaskList())
		}

		if reply.GetContinueAsNewExecutionStartToCloseTimeout() > 0 {
			continueContext = workflow.WithExecutionStartToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewExecutionStartToCloseTimeout()))
		}

		if reply.GetContinueAsNewScheduleToCloseTimeout() > 0 {
			continueContext = workflow.WithScheduleToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewScheduleToCloseTimeout()))
		}

		if reply.GetContinueAsNewScheduleToStartTimeout() > 0 {
			continueContext = workflow.WithScheduleToStartTimeout(continueContext, time.Duration(reply.GetContinueAsNewScheduleToStartTimeout()))
		}

		if reply.GetContinueAsNewStartToCloseTimeout() > 0 {
			continueContext = workflow.WithStartToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewStartToCloseTimeout()))
		}

		// Start a continue as new instance of the workflow and get the error to send
		// back to the Neon.Cadence Lib
		continueError := workflow.NewContinueAsNewError(continueContext, wectx.GetWorkflowFunction(), reply.GetContinueAsNewArgs())

		// create a new custom error and set it in the reply to be handled by the future
		cadenceError := cadenceerrors.NewCadenceError(continueError.Error(), cadenceerrors.Custom)
		reply.SetError(cadenceError)
	}

	// get the Operation corresponding the the reply
	requestID := reply.GetRequestID()
	op := Operations.Get(requestID)
	err := op.SetReply(reply, reply.GetResult())
	if err != nil {
		return err
	}

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	_ = cadenceworkflows.WorkflowContexts.Remove(contextID)
	_ = Operations.Remove(requestID)

	return nil
}

func handleWorkflowRegisterReply(reply *messages.WorkflowRegisterReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowRegisterReply Recieved", zap.Int("ProccessId", os.Getpid()))

	return nil
}

func handleNewWorkerReply(reply *messages.NewWorkerReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("NewWorkerReply Recieved", zap.Int("ProccessId", os.Getpid()))

	return nil
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

	// WorkflowContext at the specified WorflowContextID
	contextID := reply.GetContextID()
	if wectx := cadenceworkflows.WorkflowContexts.Get(contextID); wectx == nil {
		return entityNotExistError
	}

	// get the Operation corresponding the the reply
	requestID := reply.GetRequestID()
	op := Operations.Get(requestID)
	err := op.SetReply(reply, reply.GetResult())
	if err != nil {
		return err
	}

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	_ = cadenceworkflows.WorkflowContexts.Remove(contextID)
	_ = Operations.Remove(requestID)

	return nil
}

func handleWorkflowSignalReceivedReply(reply *messages.WorkflowSignalReceivedReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalReceivedReply Recieved", zap.Int("ProccessId", os.Getpid()))

	// WorkflowContext at the specified WorflowContextID
	contextID := reply.GetContextID()
	if wectx := cadenceworkflows.WorkflowContexts.Get(contextID); wectx == nil {
		return entityNotExistError
	}

	// get the Operation corresponding the the reply
	requestID := reply.GetRequestID()
	op := Operations.Get(requestID)
	err := op.SetReply(reply, nil)
	if err != nil {
		return err
	}

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	_ = cadenceworkflows.WorkflowContexts.Remove(contextID)
	_ = Operations.Remove(requestID)

	return nil
}

func handlePingReply(reply *messages.PingReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowInvokeReply Recieved", zap.Int("ProccessId", os.Getpid()))
	return nil
}
