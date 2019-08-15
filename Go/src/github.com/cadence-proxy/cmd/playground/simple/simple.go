package main

import (
	"fmt"
	"time"

	"go.uber.org/zap"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
)

/**
 * This greetings sample workflow executes 3 activities in sequential. It gets greeting and name from 2 different activities,
 * and then pass greeting and name as input to a 3rd activity to generate final greetings.
 */

// ApplicationName is the task list for this sample
const ApplicationName = "greetingsGroup"

// This is registration process where you register all your workflows
// and activity function handlers.
func init() {
	workflow.Register(SampleGreetingsWorkflow)
	activity.Register(getGreetingActivity)
	activity.Register(getNameActivity)
	activity.Register(sayGreetingActivity)
}

// SampleGreetingsWorkflow Workflow Decider.
func SampleGreetingsWorkflow(ctx workflow.Context) (string, error) {

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
