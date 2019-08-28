package main

import (
	"fmt"
	"time"

	"go.uber.org/zap"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
)

/**
 * This is a simple sample cadence workflow playground that allows you to mess around with cadence
 * workflows using the native go-client
 */

// ApplicationName is the task list for this sample
const ApplicationName = "simpleGroup"

// This is registration process where you register all your workflows
// and activity function handlers.
func init() {
	workflow.Register(SampleWorkflow)
	activity.Register(getGreetingActivity)
	activity.Register(getNameActivity)
	activity.Register(sayGreetingActivity)
}

// SampleWorkflow Workflow Decider.
func SampleWorkflow(ctx workflow.Context) (string, error) {

	// workflow sleep
	var err error
	logger := workflow.GetLogger(ctx)
	err = workflow.Sleep(ctx, time.Second*5)
	if err != nil {
		logger.Error("Sleep failed", zap.Error(err))
		return "", err
	}

	return "success", nil
}

// Get Name Activity.
func getNameActivity() (string, error) {
	return "Cadence", nil
}

// Get Greeting Activity.
func getGreetingActivity() (string, error) {
	return "Hello", nil
}

// Say Greeting Activity.
func sayGreetingActivity(greeting string, name string) (string, error) {
	result := fmt.Sprintf("Greeting: %s %s!\n", greeting, name)
	return result, nil
}
