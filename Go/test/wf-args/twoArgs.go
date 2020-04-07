package main

import (
	"context"
	"time"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"
)

func TwoArgsWorkflow(ctx workflow.Context, name1 string, name2 string) error {
	ao := workflow.ActivityOptions{
		ScheduleToStartTimeout: time.Minute,
		StartToCloseTimeout:    time.Minute,
		HeartbeatTimeout:       time.Second * 20,
	}
	ctx = workflow.WithActivityOptions(ctx, ao)

	logger := workflow.GetLogger(ctx)
	logger.Info("OneArg workflow started")
	var activityResult string
	err := workflow.ExecuteActivity(ctx, TwoArgsActivity, name1, name2).Get(ctx, &activityResult)
	if err != nil {
		logger.Error("Activity failed.", zap.Error(err))
		return err
	}

	logger.Info("Workflow completed.", zap.String("Result", activityResult))

	return nil
}

func TwoArgsActivity(ctx context.Context, name1 string, name2 string) (string, error) {
	logger := activity.GetLogger(ctx)
	logger.Info("OneArg activity started")
	return "Hello " + name1 + " & " + name2 + "!", nil
}
