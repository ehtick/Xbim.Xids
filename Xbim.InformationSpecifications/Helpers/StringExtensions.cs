﻿using System;
using System.Linq;

namespace Xbim.InformationSpecifications.Helpers
{
    /// <summary>
    /// String utility functions
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Capitalises the first character of a string.
        /// Useful when building user messages to capitalise for style.
        /// </summary>
        public static string FirstCharToUpper(this string input) =>
            input switch
            {
#if NETSTANDARD2_0
                // range operator is not available in net20
                null => throw new ArgumentNullException(nameof(input)),
                "" => "",
                _ => input.First().ToString().ToUpper() + input.Substring(1)
#else
                null => throw new ArgumentNullException(nameof(input)),
                "" => "",
                _ => input.First().ToString().ToUpper() + input[1..]
#endif
            };
    }
}
