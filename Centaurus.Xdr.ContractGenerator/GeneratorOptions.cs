using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class GeneratorOptions
    {
        [Option('a', "assembly-name", Required = true, HelpText = "The name of the assembly dll file containing contracts.")]
        public string AssemblyName { get; set; }

        [Option('l', "lang", Required = true, HelpText = "Target programming language for the contract generator. Available generators: js.")]
        public string Lang { get; set; }

        [Option('d', "dest", Required = true, HelpText = "Destination path for generated files")]
        public string Destination { get; set; }

        [Option('c', "cleanup", Required = false, HelpText = "Cleanup destination directory before build")]
        public bool CleanupDestinationDirectory { get; set; }


        [Usage(ApplicationAlias = "Centaurus.ContractGenerator")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Generate XDR contracts and converters for JS lang", new GeneratorOptions { 
                    AssemblyName = "Centaurus.Models", 
                    Lang = "js", 
                    Destination = "/srv/centaurus-js-sdk/contracts/" 
                });
            }
        }
    }
}
