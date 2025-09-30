using System;
using System.Activities;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using _2RFramework.Activities.Properties;
using System.Diagnostics;
using UiPath.Shared.Activities.Localization;
using Activity = System.Activities.Activity;
using System.Reflection;
using _2RFramework.Activities.Utilities;

namespace _2RFramework.Activities
{
    /// <summary>
    /// Represents a custom activity that executes a collection of child activities in sequence.
    /// </summary>
    [LocalizedDisplayName(nameof(Resources.Task_DisplayName))]
    [LocalizedDescription(nameof(Resources.Task_Description))]
    public class Task : NativeActivity
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the task.
        /// </summary>
        [Browsable(true)]
        [LocalizedCategory(nameof(Resources.Common_Category))]
        [DisplayName("Task Name")]
        [Description("Name of the task")]
        public InArgument<string> TaskName { get; set; }

        /// <summary>
        /// Collection of activities that will be executed by this task.
        /// This allows multiple activities without using a Sequence container.
        /// </summary>
        [Browsable(true)]
        [LocalizedCategory(nameof(Resources.Common_Category))]
        [DisplayName("Activities")]
        [Description("Collection of activities to be executed by this task")]
        public List<Activity> Activities { get; set; } = new List<Activity>();

        // Object Container: Add strongly-typed objects here and they will be available in the scope's child activities.
        private int _currentActivityIndex;

        /// <summary>
        /// Gets or sets a value indicating whether to continue on error.
        /// </summary>
        [Browsable(true)]
        [LocalizedCategory(nameof(Resources.Common_Category))]
        [DisplayName("Continue On Error")]
        [Description("If set to true, the task will continue execution with the next activity when an error occurs")]
        public bool ContinueOnError { get; set; } = true;

        #endregion


        #region Protected Methods

        /// <inheritdoc/>
        protected override void Execute(NativeActivityContext context)
        {
            // If there are no activities, just return
            if (Activities == null || Activities.Count == 0)
            {
                return;
            }

            // Reset the index and start execution
            _currentActivityIndex = 0;
            ScheduleNext(context);
        }

        /// <summary>
        /// Add CacheMetadata to include our Activities collection
        /// </summary>
        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
        }

        #endregion


        #region Helpers

        private void ScheduleNext(NativeActivityContext context)
        {
            if (_currentActivityIndex < Activities.Count)
            {
                // Schedule the activity with handlers for completion and faulting
                context.ScheduleActivity(Activities[_currentActivityIndex], OnCompleted, OnFaulted);
            }
        }

        #endregion


        #region Events

        /// <summary>
        /// In this method, we recollect task and process data to invoke 2RAgent via API, which must then try and resolve the error.
        /// If the error is resolved, we can continue to the next activity.
        /// </summary>
        /// <param name="faultContext"></param>
        /// <param name="propagatedException"></param>
        /// <param name="propagatedFrom"></param>
        private void OnFaulted(NativeActivityFaultContext faultContext, Exception propagatedException, ActivityInstance propagatedFrom)
        {
            var taskNameValue = TaskName.Get(context: faultContext);

            // We want the following properties of activities:
            // - Index
            // - Type (e.g., Write Line, If, etc.)
            // - Attributes (e.g., MessageBox text, If condition, etc.)
            var previousActivities = Activities.Take(_currentActivityIndex)
                .Select(TaskUtils.GetActivityInfo)
                .ToList();
            var failedActivity = TaskUtils.GetActivityInfo(Activities[_currentActivityIndex]);

            Console.WriteLine($"Exception in Task '{taskNameValue}', Activity #{_currentActivityIndex}: {propagatedException.Message}");
            Console.WriteLine($"Failed Activity: {failedActivity}");
            Console.WriteLine($"Previous act: {string.Join(",", previousActivities)}");

            if (ContinueOnError)
            {
                // Mark the exception as handled
                faultContext.HandleFault();
                
                // Move to the next activity
                _currentActivityIndex++;
                ScheduleNext(faultContext);
            }
            else
            {
                // If not continuing on error, do not handle the fault
                // This will propagate the exception up to the parent
            }
        }

        private void OnCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Move to the next activity and schedule it
            _currentActivityIndex++;
            ScheduleNext(context);
        } 

        #endregion
    }
}

