namespace Centaurus.Test
{
    public static class TestEnvironment
    {
        static TestEnvironment()
        {
            AlphaKeyPair = KeyPair.Random();
            Client1KeyPair = KeyPair.Random();
            Client2KeyPair = KeyPair.Random();
            Auditor1KeyPair = KeyPair.Random();
        }

        public readonly static KeyPair AlphaKeyPair;

        public readonly static KeyPair Client1KeyPair;
        public readonly static KeyPair Client2KeyPair;

        public readonly static KeyPair Auditor1KeyPair;
    }
}
