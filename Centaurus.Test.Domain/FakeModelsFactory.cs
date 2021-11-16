using Centaurus.Models;

namespace Centaurus.Test
{
    public static class FakeModelsFactory
    {
        public static TinySignature RandomSignature()
        {
            return new TinySignature
            {
                Data = 64.RandomBytes()
            };
        }
    }
}