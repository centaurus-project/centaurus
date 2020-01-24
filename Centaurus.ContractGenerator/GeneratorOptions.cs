using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class GeneratorOptions
    {
        [Option('l', "lang", Required = true, HelpText = "Target programming language for the contract generator. Available generators: js.")]
        public string Lang { get; set; }

        [Option('d', "dest", Required = true, HelpText = "Destination path for generated files")]
        public string Destination { get; set; }


        [Usage(ApplicationAlias = "Centaurus.ContractGenerator")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Generate XDR contracts and converters for JS lang", new GeneratorOptions { Lang = "js", Destination = "/srv/centaurus-js-sdk/contracts/" });
            }
        }
    }
}
