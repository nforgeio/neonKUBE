package main

import (
	"errors"

	"go.temporal.io/sdk/workflow"
)

func ErrorWorkflow(ctx workflow.Context, errorMsg string) error {
	logger := workflow.GetLogger(ctx)
	logger.Info("ErrorWorkflow workflow started")

	logger.Info("Workflow completed.")

	if errorMsg != "" {
		return errors.New(errorMsg)
	}

	return nil
}

func StringErrorWorkflow(ctx workflow.Context, name string, errorMsg string) (string, error) {
	logger := workflow.GetLogger(ctx)
	logger.Info("StringErrorWorkflow workflow started")

	logger.Info("Workflow completed.")

	if errorMsg == "" {
		return "Hello " + name + "!", nil
	}

	return name, errors.New(errorMsg)
}
