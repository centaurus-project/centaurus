using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            foreach (var prop in type.GetProperties().Where(prop => prop.IsDefined(typeof(OptionAttribute), false)))
            {
                var option = (OptionAttribute)prop.GetCustomAttributes(typeof(OptionAttribute), false).First();
                definedArgs.Add(new OptionAlias(option.LongName, option.ShortName));
            }

            return definedArgs;
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
                if (!IsArgumentName(currentArg) && i != 0) //first arg is verb (alpha or auditor)
                    throw new Exception("Invalid arguments format");

                //trim dash after check
                currentArg = currentArg.TrimStart('-');

                var currentOption = options.FirstOrDefault(x => x.LongName == currentArg || x.ShortName == currentArg);
                //create new option if not found
                if (currentOption.Equals(default(OptionAlias)))
                    currentOption = new OptionAlias(currentArg, "");

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

        static Dictionary<OptionAlias, string> GetConfigValues(string configFilePath, List<OptionAlias> optionAlias)
        {
            if (!File.Exists(configFilePath))
                throw new Exception("Config file is not found");

            using (StreamReader r = new StreamReader(configFilePath))
            {
                string json = r.ReadToEnd();
                var rawConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                var argsValues = new Dictionary<OptionAlias, string>();

                foreach (var configField in rawConfig.Keys)
                {
                    var val = rawConfig[configField];
                    if (string.IsNullOrEmpty(val))
                        continue;

                    var currentOptionAlias = optionAlias.FirstOrDefault(o => o.LongName == configField);
                    if (!currentOptionAlias.Equals(default(OptionAlias)))
                        argsValues.Add(currentOptionAlias, val);
                }

                return argsValues;
            }
        }

        static string[] MergeArgs<T>(string[] passedArgs)
        {
            var optionAlias = GetOptions<T>();
            var envArgsDict = GetEnvironmentValues(optionAlias);
            var passedArgsDict = GetArguments(passedArgs, optionAlias);

            var configFileOptionAlias = new OptionAlias(BaseSettings.ConfigFileArgName, "");
            var configFilePath = "";
            var configFileArgsDict = new Dictionary<OptionAlias, string>();

            if (passedArgsDict.TryGetValue(configFileOptionAlias, out configFilePath) || envArgsDict.TryGetValue(configFileOptionAlias, out configFilePath))
                configFileArgsDict = GetConfigValues(configFilePath, optionAlias);

            //override config by env variables
            var merged = OverrideValues(configFileArgsDict, envArgsDict);
            //override config and env variables by passed arguments
            merged = OverrideValues(merged, passedArgsDict);

            return merged
                .SelectMany(a => new string[] { "--" + a.Key.LongName, a.Value })
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
            public OptionAlias(string longName, string shortName)
            {
                LongName = longName;
                ShortName = shortName;
            }

            public string LongName { get; }
            public string ShortName { get; }

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
                return $"Short: {ShortName}, Long: {LongName}, Env: {EnvironmentLongName}";
            }
        }
    }
}
