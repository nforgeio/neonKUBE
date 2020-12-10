package main

import (
	"context"
	"time"

	"go.temporal.io/sdk/activity"
	"go.temporal.io/sdk/workflow"
	"go.uber.org/zap"
)

func ArrayArgsWorkflow(ctx workflow.Context, array []int32, input string) ([]int32, error) {
	ao := workflow.ActivityOptions{
		ScheduleToStartTimeout: time.Minute,
		StartToCloseTimeout:    time.Minute,
		HeartbeatTimeout:       time.Second * 30,
	}
	ctx = workflow.WithActivityOptions(ctx, ao)

	logger := workflow.GetLogger(ctx)
	logger.Info("ArrayArgs workflow started")

	var activityResult []int32

	err := workflow.ExecuteActivity(ctx, ArrayArgsActivity, array, input).Get(ctx, &activityResult)
	if err != nil {
		logger.Error("Activity failed.", zap.Error(err))
		return []int32{}, err
	}

	logger.Info("Workflow completed.")

	return activityResult, nil
}

func ArrayArgsActivity(ctx context.Context, array []int32, input string) ([]int32, error) {
	logger := activity.GetLogger(ctx)
	logger.Info("ArrayArgs activity started")
	return array, nil
}
