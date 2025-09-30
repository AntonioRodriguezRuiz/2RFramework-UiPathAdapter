using System;
using System.Collections.Generic;
using System.Activities;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Activity = System.Activities.Activity;

namespace _2RFramework.Activities.Utilities
{
    internal class TaskUtils
    {
        public static string GetActivityInfo(Activity activity)
        {
            // This is the method we want to debug
            var type = activity.GetType();
            Console.WriteLine($"Inspecting activity: {activity.DisplayName} ({type.FullName})");

            var propDict = new List<object>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object? rawValue = prop.GetValue(activity);

                // Handle InArgument<T>
                if (rawValue is Argument arg)
                {
                    Console.WriteLine($"  Property '{prop.Name}' is an Argument. Extracted value: {rawValue ?? "<null>"}");
                    rawValue = TryGetLiteralFromArgument(arg);
                    Console.WriteLine($"  Property '{prop.Name}' is an Argument. Extracted value: {rawValue ?? "<null>"}");
                }

                propDict.Add(new {
                    PropertyName = prop.Name,
                    PropertyType = prop.PropertyType.FullName ?? "unknown",
                    Value = rawValue ?? "<null>"
                });
            }

            return string.Join(",", propDict);
        }

        private static object? TryGetLiteralFromArgument(Argument arg)
        {
            // 0 will always be the Expression. We don't do GetProperty("Expression") because its duplicated
            var exprProp = arg.GetType().GetProperties()[0]; 
            var exprObj = exprProp.GetValue(arg);
            if (exprObj == null) return null;

            // Return the object if it's a Literal<T>
            if (exprObj.GetType().GetProperty("Value") != null)
                return exprObj.GetType().GetProperty("Value").GetValue(exprObj).ToString();

            // Look for "ExpressionText" property on the expression, should work for text-type and selection entries
            var valueProp = exprObj.GetType().GetProperty("ExpressionText");

            return valueProp?.GetValue(exprObj);
        }
    }
}
