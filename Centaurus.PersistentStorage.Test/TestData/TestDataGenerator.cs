using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.PersistentStorage
{
    public static class TestDataGenerator
    {
        private static List<byte[]> Accounts;

        public const int QuantaPerBatch = 5000; //how many quanta included in the write batch
        public const int EffectsPerQuantum = 2; //how many effects each quantum yields
        public const int AccountsAffected = 500; //how many accounts get updated as a result of the batch application

        static TestDataGenerator()
        {
            Accounts = new List<byte[]>();
            var totalExistingAccounts = 5000;
            for (var i = 0; i < totalExistingAccounts; i++)
            {
                Accounts.Add(RandomData(32));
            }
            Console.WriteLine("=== Test data generator params ===");
            Console.WriteLine("Sample test accounts:");
            Console.WriteLine("  " + HexConverter.BufferToHex(Accounts[0]));
            Console.WriteLine("  " + HexConverter.BufferToHex(Accounts[1]));
            Console.WriteLine("Batch params:");
            Console.WriteLine("  Quanta written per batch: " + QuantaPerBatch);
            Console.WriteLine("  Effects written per batch: " + QuantaPerBatch * EffectsPerQuantum);
            Console.WriteLine("  Accounts updated per batch: " + AccountsAffected);
            Console.WriteLine("=====================");
        }

        public static List<IPersistentModel> GenerateTestData(int prefix)
        {
            var testData = new List<IPersistentModel>();

            var auditors = GenerateBuffers(19, 32);

            Generate(QuantaPerBatch, testData,
                i => new QuantumPersistentModel()
                {
                    Apex = (ulong)(i + prefix),
                    TimeStamp = 100 * i,
                    RawQuantum = RandomData(64),
                    Effects = GenerateBuffers(4, 40).Select((b, i) => new AccountEffects { Account = (ulong)i, Effects = b }).ToList(),
                    Signatures = auditors.Select(_ => RandomData(64)).Select(s => new SignatureModel { PayloadSignature = s }).ToList()
                });
            Generate(QuantaPerBatch * EffectsPerQuantum, testData,
                i => new QuantumRefPersistentModel() { AccountId = (ulong)rnd.Next(1, 1000), Apex = (ulong)(i + prefix) });
            Generate(AccountsAffected, testData,
                i => new AccountPersistentModel()
                {
                    AccountPubkey = GetRandomAccount(),
                    Nonce = (ulong)(i + prefix),
                    Balances = new List<BalancePersistentModel>
                    {
                        new BalancePersistentModel{ Amount=(ulong)i, Asset = "USD"},
                        new BalancePersistentModel{ Amount=(ulong)i+20, Asset = "XLM"}
                    },
                    Orders = new List<OrderPersistentModel>
                    {
                        new OrderPersistentModel{Amount = (ulong)i+10, Apex = (ulong)i, Price = 1000/i, QuoteAmount = (ulong)i},
                        new OrderPersistentModel{Amount = (ulong)i+40, Apex = (ulong)i+2, Price = 5000/i, QuoteAmount = (ulong)i}
                    }
                });
            return testData;
        }

        private static byte[] GetRandomAccount()
        {
            return Accounts[rnd.Next(Accounts.Count)];
        }

        private static void Generate(int cnt, List<IPersistentModel> testData, Func<int, IPersistentModel> generate)
        {
            for (var i = 1; i <= cnt; i++)
            {
                testData.Add(generate(i));
            }
        }

        private static Random rnd = new Random(456345645);

        private static byte[] RandomData(int length)
        {
            var res = new byte[length];
            rnd.NextBytes(res);
            return res;
        }

        private static List<byte[]> GenerateBuffers(int count, int bufferLength)
        {
            var res = new List<byte[]>(count);
            for (var i = 0; i < count; i++)
            {
                res.Add(RandomData(bufferLength));
            }

            return res;
        }
    }
}
