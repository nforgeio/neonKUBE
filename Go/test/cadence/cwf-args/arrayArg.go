package main

import (
	"context"
	"time"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"
)

func ArrayArgWorkflow(ctx workflow.Context, array []int32) ([]int32, error) {
	ao := workflow.ActivityOptions{
		ScheduleToStartTimeout: time.Minute,
		StartToCloseTimeout:    time.Minute,
		HeartbeatTimeout:       time.Second * 30,
	}
	ctx = workflow.WithActivityOptions(ctx, ao)

	logger := workflow.GetLogger(ctx)
	logger.Info("ArrayArg workflow started")

	var activityResult []int32

	err := workflow.ExecuteActivity(ctx, ArrayArgActivity, array).Get(ctx, &activityResult)
	if err != nil {
		logger.Error("Activity failed.", zap.Error(err))
		return []int32{}, err
	}

	logger.Info("Workflow completed.")

	return activityResult, nil
}

func ArrayArgActivity(ctx context.Context, array []int32) ([]int32, error) {
	logger := activity.GetLogger(ctx)
	logger.Info("ArrayArg activity started")
	return array, nil
}
