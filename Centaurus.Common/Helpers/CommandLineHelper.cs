using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus
{
    public static class CommandLineHelper
    {
        /// <summary>
        /// Mergers passed arguments and environment variables
        /// </summary>
        /// <typeparam name="T">Class that describes expected arguments</typeparam>
        /// <param name="passedArgs">Passed arguments</param>
        /// <returns>Merged arguments</returns>
        public static string[] GetMergedArgs<T>(string[] passedArgs)
        {
            return MergeArgs<T>(passedArgs);
        }

        static List<OptionAlias> GetOptions<T>()
        {
            var type = typeof(T);
            var definedArgs = new List<OptionAlias>();
            if (TryGetVerb<T>(out OptionAlias alias))
                definedArgs.Add(alias);
            foreach (var prop in type.GetProperties().Where(prop => prop.IsDefined(typeof(OptionAttribute), false)))
            {
                var option = (OptionAttribute)prop.GetCustomAttributes(typeof(OptionAttribute), false).First();
                definedArgs.Add(new OptionAlias(option.LongName, option.ShortName));
            }

            return definedArgs;
        }

        static bool TryGetVerb<T>(out OptionAlias alias)
        {
            alias = default;
            var type = typeof(T);
            if (type.IsDefined(typeof(VerbAttribute), false))
            {
                var verb = (VerbAttribute)type.GetCustomAttributes(typeof(VerbAttribute), true).First();
                alias = new OptionAlias(verb.Name, "", true);
                return true;
            }
            return false;
        }

        static Dictionary<OptionAlias, string> GetEnvironmentValues(List<OptionAlias> options)
        {
            var data = new Dictionary<OptionAlias, string>();
            foreach (var option in options)
            {
                var val = Environment.GetEnvironmentVariable(option.EnvironmentLongName);
                if (!string.IsNullOrEmpty(val))
                    data.Add(option, val);
            }

            return data;
        }

        static Dictionary<OptionAlias, string> GetArguments(string[] args, List<OptionAlias> options)
        {
            var data = new Dictionary<OptionAlias, string>();
            var i = 0;
            while (i < args.Length)
            {
                var currentArg = args[i];
                if (!IsArgumentName(currentArg) && i != 0) // first arg should be verb (alpha or auditor)
                    throw new Exception("Invalid arguments format");

                //trim dash after check
                currentArg = currentArg.TrimStart('-');

                var currentOption = options.FirstOrDefault(x => x.LongName == currentArg || x.ShortName == currentArg);
                //create new option if not found
                if (currentOption.Equals(default(OptionAlias)))
                    currentOption = new OptionAlias(currentArg, "", i == 0); // first arg should be verb

                string currentArgValue = null;
                //check if next argument exists
                if (i + 1 < args.Length)
                {
                    //check if next argument is current argument value
                    var nextArgument = args[i + 1];
                    if (!IsArgumentName(nextArgument))
                    {
                        currentArgValue = nextArgument;
                        //we already processed the value
                        i++;
                    }
                }

                data.Add(currentOption, currentArgValue);
                i++;
            }
            return data;
        }

        /// <summary>
        /// Checks if specified argument is argument name or argument value
        /// </summary>
        /// <param name="arg">Argument to check</param>
        /// <returns></returns>
        static bool IsArgumentName(string arg)
        {
            return arg.IndexOf('-') == 0;
        }

        static string[] MergeArgs<T>(string[] passedArgs)
        {
            var optionAlias = GetOptions<T>();
            var envArgsDict = GetEnvironmentValues(optionAlias);
            var passedArgsDict = GetArguments(passedArgs, optionAlias);

            //override config and env variables by passed arguments
            var merged = OverrideValues(passedArgsDict, envArgsDict);

            return merged
                .SelectMany(a => new string[] { a.Key.FullArgumentName, a.Value })
                .Where(a => a != null)
                .ToArray();
        }

        static Dictionary<OptionAlias, string> OverrideValues(Dictionary<OptionAlias, string> target, Dictionary<OptionAlias, string> newValues)
        {
            foreach (var key in newValues.Keys)
                target[key] = newValues[key];
            return target;
        }

        struct OptionAlias
        {
            public OptionAlias(string longName, string shortName, bool isVerb = false)
            {
                LongName = longName;
                ShortName = shortName;
                IsVerb = isVerb;
            }

            public string LongName { get; }
            public string ShortName { get; }
            public bool IsVerb { get; }

            public string EnvironmentLongName
            {
                get
                {
                    return LongName.ToUpper();
                }
            }

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj.GetType() != GetType()) return false;
                OptionAlias other = (OptionAlias)obj;
                return other.LongName == LongName && other.ShortName == ShortName;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(LongName, ShortName);
            }

            public override string ToString()
            {
                return $"Short: {ShortName}, Long: {LongName}, Env: {EnvironmentLongName}, IsVerb: {IsVerb}";
            }

            public string FullArgumentName
            {
                get
                {
                    return IsVerb ? LongName : $"--{LongName}";
                }
            }
        }
    }
}
