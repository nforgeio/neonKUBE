package main

import (
	"context"
	"time"

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
	activity.Register(waitForCompletionActivity)
}

// SampleWorkflow Workflow Decider.
func SampleWorkflow(ctx workflow.Context) (string, error) {
	logger := workflow.GetLogger(ctx)
	logger.Info("Workflow started")

	heartbeatDuration := time.Second * 2
	ao := workflow.ActivityOptions{
		ActivityID:             "wait-for-completion",
		ScheduleToStartTimeout: time.Minute,
		StartToCloseTimeout:    time.Minute,
		HeartbeatTimeout:       heartbeatDuration,
		WaitForCancellation:    true,
	}

	ctx = workflow.WithActivityOptions(ctx, ao)
	activityCtx, cancel := workflow.WithCancel(ctx)
	future := workflow.ExecuteActivity(activityCtx, waitForCompletionActivity)
	workflow.Sleep(ctx, 5*time.Second)
	cancel()

	var result string
	err := future.Get(ctx, &result)
	workflow.Sleep(ctx, 10*time.Second)

	return result, err
}

// Activity to be externally completed.
func waitForCompletionActivity(ctx context.Context) error {
	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(time.Second * 1):
			activity.RecordHeartbeat(ctx, "I am alive")
		}
	}
}
