package main

import (
	"errors"
	"fmt"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
)

const ApplicationName = "simpleGroup"

func init() {
	workflow.Register(RetryWorkflow)
	activity.Register(getNameActivity)
}

func RetryWorkflow(ctx workflow.Context) (string, error) {

	if workflow.IsReplaying(ctx) {
		fmt.Println("IsReplaying: TRUE")
	} else {
		fmt.Println("IsReplaying: FALSE")
	}

	return "", errors.New("Force replay.")

	/*
		// workflow sleep
		var err error
		logger := workflow.GetLogger(ctx)

		err = workflow.Sleep(ctx, time.Second*5)
		if err != nil {
			logger.Error("Sleep failed", zap.Error(err))
			return "", err
		}

		return "success", nil
	*/
}

func getNameActivity() (string, error) {
	return "Cadence", nil
}
