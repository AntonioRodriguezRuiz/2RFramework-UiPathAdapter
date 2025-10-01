using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System;
using System.Activities;
using System.Activities.Hosting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Activity = System.Activities.Activity;

namespace _2RFramework.Activities.Utilities
{
    internal class TaskUtils
    {
        public static List<object> GetActivityInfo(Activity activity, Dictionary<string, object?> variables)
        {
            var excludedProps = new List<string>(){"Result", "ResultType", "Id"};
            var type = activity.GetType();
            var activityInfo = new List<object>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object? rawValue = prop.GetValue(activity);
                if (excludedProps.Contains(prop.Name))
                    continue;

                // Handle InArgument<T>
                if (rawValue is Argument arg)
                    rawValue = TryGetLiteralFromArgument(arg, variables);
                if (rawValue == null)
                    continue;


                if (prop.Name == "DisplayName")
                    // We add the activity type at the top of the list
                    activityInfo.Insert(0, new {
                                ActivityType = rawValue,
                            });
                else
                    activityInfo.Add(new {
                                PropertyName = prop.Name,
                                Value = rawValue,
                            });
            }

            return activityInfo;
        }

        private static object? TryGetLiteralFromArgument(Argument arg, Dictionary<string, object?> variables)
        {
            // 0 will always be the Expression. We don't do GetProperty("Expression") because its duplicated
            var exprProp = arg.GetType().GetProperties()[0]; 
            var exprObj = exprProp.GetValue(arg);
            if (exprObj == null) return null;

            // Return the object if it's a Literal<T>
            if (exprObj.GetType().GetProperty("Value") != null)
                return exprObj.GetType().GetProperty("Value")?.GetValue(exprObj)?.ToString();

            // Look for "ExpressionText" property on the expression, should work for text-type and selection entries
            var valueProp = exprObj.GetType().GetProperty("ExpressionText");
            if (valueProp == null) return null;
            
            // Get the ExpressionText value
            var expressionText = valueProp.GetValue(exprObj) as string;
            if (string.IsNullOrEmpty(expressionText)) return null;
            
            // Differentiate string from variable reference
            // In UiPath:
            // - String literals are wrapped in double quotes: "text"
            // - Variable references are not wrapped in quotes: variableName
            if (expressionText.StartsWith("\"") && expressionText.EndsWith("\""))
            {
                return expressionText;
            }
            else
            {
                return new
                {
                    Type = "VariableReference",
                    VariableName = expressionText,
                    Value = variables.GetValueOrDefault(expressionText)
                };
            }
        }

        public class WorkflowInstanceInfo : IWorkflowInstanceExtension
        {
            private WorkflowInstanceProxy _proxy;

            public IEnumerable<object> GetAdditionalExtensions()
            {
                yield break;
            }

            public void SetInstance(WorkflowInstanceProxy instance) => this._proxy = instance;

            public WorkflowInstanceProxy GetProxy() => this._proxy;
        }

        public static IEnumerable<Activity> GetDescendants(Activity root, bool recursive)
        {
            yield return root;
            foreach (var child in WorkflowInspectionServices.GetActivities(root))
            {
                if (recursive)
                    foreach (var d in GetDescendants(child, true))
                        yield return d;
                else
                    yield return child;
            }
        }

        /// <summary>
        /// Gets all workflow variables from the main sequence as a dictionary
        /// </summary>
        /// <param name="context">The native activity context</param>
        /// <returns>Dictionary of variable names and their values</returns>
        public static Dictionary<string, object?> GetWorkflowVariables(NativeActivityContext context)
        {
            var workflowVariables = new Dictionary<string, object?>();
            
            var workflowActivities = GetDescendants(
                context.GetExtension<WorkflowInstanceInfo>().GetProxy().WorkflowDefinition, 
                false);
            
            // Main sequence should be the last activity under workflowActivities
            var mainSequence = workflowActivities.LastOrDefault();

            if (mainSequence == null) return workflowVariables;
            foreach (var local in mainSequence.GetLocals())
            {
                var variable = local.GetType().GetProperties()[0].GetValue(local); // Duplicated property name
                    
                var value = variable?.GetType().GetProperty("Value")?.GetValue(variable);
                workflowVariables.Add(local.Name, value);
            }

            return workflowVariables;
        }
    }
}
