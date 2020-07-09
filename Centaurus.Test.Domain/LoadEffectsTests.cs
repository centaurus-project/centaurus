using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class LoadEffectsTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            GlobalInitHelper.DefaultAlphaSetup();
            MessageHandlers<AlphaWebSocketConnection>.Init();
        }

        static object[] EffectsLoadTestCases = new object[]
        {
            new object[] { null, false },
            new object[] { null, true },
            new object[] { TestEnvironment.Client1KeyPair, false },
            new object[] { TestEnvironment.Client2KeyPair, true }
        };

        [Test]
        [TestCaseSource(nameof(EffectsLoadTestCases))]
        public async Task LoadEffectsTest(KeyPair accountKey, bool isDesc)
        {
            var allLimit = 1000;
            var allEffectsResult = await LoadAllEffects(null, isDesc, allLimit, accountKey);

            var opositeOrderedResult = await LoadAllEffects(null, !isDesc, allLimit, accountKey);

            //check ordering
            for (int i = 0, opI = allEffectsResult.Count - 1; i < allEffectsResult.Count; i++, opI--)
            {
                var leftEffect = allEffectsResult[i];
                var rightEffect = opositeOrderedResult[opI];
                var areEqual = ByteArrayPrimitives.Equals(leftEffect.ComputeHash(), rightEffect.ComputeHash());
                Assert.AreEqual(true, areEqual, "Ordering doesn't work as expected.");
            }

            //check fetching
            var limit = 1;
            await TestFetching(allEffectsResult, new byte[12].ToHex(), isDesc, limit, accountKey);

            //check reverse fetching
            await TestFetching(allEffectsResult, new byte[12].ToHex(), !isDesc, limit, accountKey, true);
        }

        private async Task<List<Effect>> LoadAllEffects(string cursor, bool isDesc, int limit, KeyPair keyPair)
        {
            var response = (await Global.SnapshotManager.LoadEffects(cursor, isDesc, limit, null)).Items;
            if (keyPair == null)
                return response;
            return response
                .Where(e => ByteArrayPrimitives.Equals(e.Pubkey?.Data, keyPair?.PublicKey))
                .ToList();
        }

        private async Task TestFetching(List<Effect> allEffects, string cursor, bool isDesc, int limit, KeyPair account, bool isReverseDirection = false)
        {
            var nextCursor = cursor;
            var increment = isReverseDirection ? -1 : 1;
            var index = isReverseDirection ? allEffects.Count - 1 : 0;
            var totalCount = 0;
            while (nextCursor != null)
            {
                var currentEffectsResult = await Global.SnapshotManager.LoadEffects(nextCursor, isDesc, limit, account?.PublicKey);
                if (totalCount == allEffects.Count)
                {
                    Assert.AreEqual(0, currentEffectsResult.Items.Count, "Some extra effects were loaded.");
                    nextCursor = null;
                }
                else
                {
                    Assert.AreEqual(1, currentEffectsResult.Items.Count, "Effects are not loaded.");
                    var areEqual = ByteArrayPrimitives.Equals(allEffects[index].ComputeHash(), currentEffectsResult.Items[0].ComputeHash());
                    Assert.AreEqual(true, areEqual, "Effects are not equal.");
                    index += increment;
                    totalCount++;
                    nextCursor = currentEffectsResult.NextToken;
                }
            }
            Assert.AreEqual(allEffects.Count, totalCount, "Effects total count are not equal.");
        }
    }
}
