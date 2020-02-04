using Centaurus.Xdr;
using CommandLine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Centaurus.ContractGenerator
{
    class Program
    {
        static void WriteDelimiter()
        {
            Console.WriteLine(new string('=', 20));
        }

        static void Main(string[] args)
        {
            try
            {
                Parser.Default.ParseArguments<GeneratorOptions>(args)
                    .WithParsed(options =>
                    {
                        Console.WriteLine($"Generating {options.Lang} XDR contract files. Destination path: {options.Destination}");
                        ContractGenerator generator;
                        switch (options.Lang)
                        {
                            case "js":
                            case "JS":
                                generator = new JavaScriptContractGenerator();
                                break;
                            case "cs":
                            case "CS":
                                generator = new CSharpContractGenerator(Path.GetFileNameWithoutExtension(options.AssemblyName));
                                break;
                            default:
                                throw new Exception($"Failed to find XDR contracts generator for lang {options.Lang}.");
                        }

                        var directoryInfo = new DirectoryInfo(options.Destination);
                        if (!directoryInfo.Exists)
                        {
                            Directory.CreateDirectory(options.Destination);
                        }

                        if (options.CleanupDestinationDirectory)
                        {
                            foreach (var file in directoryInfo.GetFiles())
                            {
                                file.Delete();
                            }
                        }

                        /*var contractsModulePath = Path.Combine(Environment.CurrentDirectory, options.AssemblyName);
                        if (!contractsModulePath.EndsWith(".dll"))
                        {
                            contractsModulePath += ".dll";
                        }*/
                        var contractsAssembly = Assembly.LoadFrom(options.AssemblyName);

                        var contracts = XdrSerializationTypeMapper.DiscoverXdrContracts(contractsAssembly);
                        generator.LoadContracts(contracts);
                        var bundle = generator.Generate();
                        bundle.Save(options.Destination);
                        WriteDelimiter();
                        Console.WriteLine("Exported files:");
                        foreach (var file in bundle.Files)
                        {
                            Console.WriteLine("  " + file.FileName);
                        }
                        WriteDelimiter();
                        Console.WriteLine("Done");
                    })
                    .WithNotParsed(errors =>
                    {
                        var meaningfulErrors = errors.Where(e => !e.StopsProcessing).ToList();
                        if (meaningfulErrors.Count > 0)
                        {
                            WriteDelimiter();
                            Console.WriteLine("Invalid arguments:");
                            foreach (Error err in errors)
                            {
                                Console.WriteLine("  " + err.ToString());
                            }
                        }
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine(e is AggregateException ? e.InnerException : e);
            }
        }
    }
}
