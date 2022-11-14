﻿using System;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace Microsoft.ApplicationInsights.ServiceFabric.Module
{
    /// <summary>
    /// Utility functions for dealing with exceptions.
    /// </summary>
    internal class ExceptionUtilities
    {
        /// <summary>
        /// Get the string representation of this Exception with special handling for AggregateExceptions.
        /// </summary>
        /// <param name="ex">The exception to convert to a string.</param>
        /// <returns>The detailed string version of the provided exception.</returns>
        internal static string GetExceptionDetailString(Exception ex)
        {
            var ae = ex as AggregateException;
            if (ae != null)
            {
                return ae.Flatten().InnerException.ToInvariantString();
            }

            return ex.ToInvariantString();
        }
    }
}
