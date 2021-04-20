using Centaurus.DAL;
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
            context = GlobalInitHelper.DefaultAlphaSetup().Result;
        }

        static object[] EffectsLoadTestCases = new object[]
        {
            new object[] { TestEnvironment.Client1KeyPair, false },
            new object[] { TestEnvironment.Client2KeyPair, true }
        };
        private AlphaContext context;

        [Test]
        [TestCaseSource(nameof(EffectsLoadTestCases))]
        public async Task LoadEffectsTest(KeyPair accountKey, bool isDesc)
        {
            var account = context.AccountStorage.GetAccount(accountKey);

            var allLimit = 1000;
            var allEffectsResult = (await context.PersistenceManager.LoadEffects(null, isDesc, allLimit, account.Account.Id)).Items;

            var opositeOrderedResult = (await context.PersistenceManager.LoadEffects(null, !isDesc, allLimit, account.Account.Id)).Items;

            //check ordering
            for (int i = 0, opI = allEffectsResult.Count - 1; i < allEffectsResult.Count; i++, opI--)
            {
                var leftEffect = allEffectsResult[i].Items;
                var rightEffect = opositeOrderedResult[opI].Items;
                var effectsCount = leftEffect.Count;
                for (int c = 0, opC = effectsCount - 1; c < effectsCount; c++, opC--)
                {
                    var areEqual = ByteArrayPrimitives.Equals(leftEffect[c].ComputeHash(), rightEffect[opC].ComputeHash());
                    Assert.AreEqual(true, areEqual, "Ordering doesn't work as expected.");
                }
            }

            var zeroCursor = "0";
            //check fetching
            var limit = 1;
            await TestFetching(allEffectsResult, zeroCursor, isDesc, limit, account.Account.Id);

            //check reverse fetching
            allEffectsResult.Reverse();
            allEffectsResult.ForEach(ae => ae.Items.Reverse());
            await TestFetching(allEffectsResult, zeroCursor, !isDesc, limit, account.Account.Id, true);
        }

        private async Task TestFetching(List<ApexEffects> allEffects, string cursor, bool isDesc, int limit, int account, bool isReverseDirection = false)
        {
            var nextCursor = cursor;
            var totalCount = 0;
            while (nextCursor != null)
            {
                var currentEffectsResult = await context.PersistenceManager.LoadEffects(nextCursor, isDesc, limit, account);
                if (totalCount == allEffects.Count)
                {
                    Assert.AreEqual(0, currentEffectsResult.Items.Count, "Some extra effects were loaded.");
                    nextCursor = null;
                }
                else
                {
                    Assert.AreEqual(1, currentEffectsResult.Items.Count, "Effects are not loaded.");
                    var apexEffects = currentEffectsResult.Items[0].Items;
                    for (var i = 0; i < apexEffects.Count; i++)
                    {
                        var areEqual = ByteArrayPrimitives.Equals(allEffects[totalCount].Items[i].ComputeHash(), apexEffects[i].ComputeHash());
                        Assert.AreEqual(true, areEqual, "Effects are not equal.");
                    }
                    totalCount++;
                    nextCursor = currentEffectsResult.NextPageToken;
                }
            }
            Assert.AreEqual(allEffects.Count, totalCount, "Effects total count are not equal.");
        }
    }
}
