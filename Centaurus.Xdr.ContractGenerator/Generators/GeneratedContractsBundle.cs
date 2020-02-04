using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class GeneratedContractsBundle
    {
        public List<GeneratedContractFile> Files { get; private set; } = new List<GeneratedContractFile>();

        public void Add(IEnumerable<GeneratedContractFile> generatedFiles)
        {
            Files.AddRange(generatedFiles);
        }

        public void Add(GeneratedContractFile generatedFile)
        {
            Files.Add(generatedFile);
        }

        public void Save(string baseDirectory)
        {
            foreach (var file in Files)
            {
                file.Save(baseDirectory);
            }
        }
    }
}
