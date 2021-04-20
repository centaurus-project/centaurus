using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    [TestFixture]
    public class MockWebSocketTests
    {

        [Test]
        public async Task TestWebsocket()
        {
            var leftWebsocket = new MockWebSocket();
            var rightWebsocket = new MockWebSocket();
            leftWebsocket.Connect(rightWebsocket);
            rightWebsocket.Connect(leftWebsocket);

            var data = GetTestData();
            await rightWebsocket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
            var buffer = new byte[1024];
            var segment = buffer.AsMemory();
            var res = await leftWebsocket.ReceiveAsync(segment, CancellationToken.None);
            Assert.AreEqual(data.Length, res.Count);
            Assert.IsTrue(data.SequenceEqual(segment.Slice(0, res.Count).ToArray()));
        }

        private byte[] GetTestData()
        {
            var r = new Random();

            return Enumerable.Range(0, r.Next(128, 1024)).Select(i => (byte)r.Next(0, 255)).ToArray();
        }
    }
}
