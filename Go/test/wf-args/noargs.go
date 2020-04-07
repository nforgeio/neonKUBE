package main

import (
	"context"
	"time"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"
)

func NoArgsWorkflow(ctx workflow.Context) error {
	ao := workflow.ActivityOptions{
		ScheduleToStartTimeout: time.Minute,
		StartToCloseTimeout:    time.Minute,
		HeartbeatTimeout:       time.Second * 20,
	}
	ctx = workflow.WithActivityOptions(ctx, ao)

	logger := workflow.GetLogger(ctx)
	logger.Info("NoArg workflow started")
	var activityResult string
	err := workflow.ExecuteActivity(ctx, NoArgsActivity).Get(ctx, &activityResult)
	if err != nil {
		logger.Error("Activity failed.", zap.Error(err))
		return err
	}

	logger.Info("Workflow completed.", zap.String("Result", activityResult))

	return nil
}

func NoArgsActivity(ctx context.Context) (string, error) {
	logger := activity.GetLogger(ctx)
	logger.Info("NoArg activity started")
	return "Hello there!", nil
}
