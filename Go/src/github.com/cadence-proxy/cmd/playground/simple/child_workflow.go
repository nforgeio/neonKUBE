package main

import (
	"time"

	"go.uber.org/cadence/workflow"
)

/**
 * This sample workflow demonstrates how to use invoke child workflow from parent workflow execution.  Each child
 * workflow execution is starting a new run and parent execution is notified only after the completion of last run.
 */

// This is registration process where you register all your workflows
// and activity function handlers.
func init() {
	workflow.Register(SampleChildWorkflow)
}

// SampleChildWorkflow workflow decider
func SampleChildWorkflow(ctx workflow.Context, t time.Duration) (string, error) {
	logger := workflow.GetLogger(ctx)
	err := workflow.Sleep(ctx, t)
	if err != nil {
		logger.Error("Child Workflow Sleep Failed.")
		return "", err
	}

	return "Success", nil
}
