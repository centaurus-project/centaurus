using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Centaurus.ContractGenerator
{
    internal static class CaseConversionUtils
    {
        public static string ConvertToCamelCase(string str)
        {
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        public static string ConvertPascalCaseToKebabCase(string str)
        {
            var builder = new StringBuilder(str.Length)
                .Append(char.ToLower(str[0]));

            foreach (var c in str.Substring(1))
            {
                if (char.IsUpper(c))
                {
                    builder.Append('-');
                    builder.Append(char.ToLower(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static string ConvertKebabCaseToPascalCase(string str)
        {
            var builder = new StringBuilder(str.Length);
            foreach (var c in str.Split('-'))
            {
                builder.Append(char.ToUpperInvariant(c[0]) + c.Substring(1));
            }

            return builder.ToString();
        }
    }
}
