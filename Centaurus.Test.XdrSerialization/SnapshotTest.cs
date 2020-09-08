using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    class SnapshotTest
    {

        //[Test]
        //public void SnapshotComputeHashTest()
        //{
        //    var snapshot = new Snapshot();

        //    var pubkey = new byte[32];
        //    Array.Fill(pubkey, (byte)10);

        //    var testClients = new string[] { "SCR6C6STGV7RKURFGV7XA76WRUZYAKRYFICLWBHTI7NAW3VXVRA5T75E", "SBMZHCOQF2SANK2HSCMEZTOCJKBXV6CYRLAEE66BWSQBLKOZXLNMQN3T" };

        //    var accs = new List<Models.Account>();
        //    foreach (var test in testClients)
        //    {
        //        var kp = KeyPair.FromSecretSeed(test);

        //        var acc = new Models.Account
        //        {
        //            Pubkey = new RawPubKey() { Data = kp.PublicKey },
        //            Balances = new List<Balance>()
        //        };
        //        acc.Balances.Add(new Balance { Amount = (long)1000 * 10_000_000, Account = acc });
        //        acc.Balances.Add(new Balance { Amount = (long)1000 * 10_000_000, Asset = 1, Account = acc });

        //        accs.Add(acc);
        //    }

        //    snapshot.Accounts = accs;
        //    snapshot.Orders = new List<Order>();
        //}
    }
}
