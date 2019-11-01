using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test.Contracts
{
    [XdrUnion((int)TetrapodClasses.Amphibian, typeof(Amphibian))]
    [XdrUnion((int)TetrapodClasses.Mammal, typeof(Mammal))]
    [XdrUnion(3, typeof(Reptile))]
    [XdrUnion(4, typeof(Bird))]
    public class Tetrapod: Vertebrate
    {
        [XdrField(0, Optional = true)]
        public string Habitat { get; set; }
    }

    enum TetrapodClasses
    {
        Amphibian = 1,
        Mammal = 2,
        Reptile = 3,
        Bird = 4
    }
}
