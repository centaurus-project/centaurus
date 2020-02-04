using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class GeneratedContractFile
    {
        public GeneratedContractFile(string fileName, string contents)
        {
            FileName = fileName;
            Contents = contents;
        }
        public readonly string FileName;

        public readonly string Contents;

        public void Save(string baseDirectory)
        {
            File.WriteAllText(Path.Combine(baseDirectory, FileName), Contents);
        }

        public override string ToString()
        {
            return "File " + FileName;
        }
    }
}
