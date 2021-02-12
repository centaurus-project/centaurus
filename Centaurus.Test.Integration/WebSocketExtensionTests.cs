using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    [TestFixture]
    public class WebSocketExtensionTests
    {
        [Test]
        public async Task GetInputStreamReaderTest()
        {
            var result = await new FakeWebSocket(Enumerable.Repeat((byte)1, 10240).ToArray()).GetWebsocketBuffer(20480, CancellationToken.None);
            using (result.messageBuffer)
            {
                Assert.AreEqual(10240, result.messageBuffer.Length);
            }
            result = await new FakeWebSocket(Enumerable.Repeat((byte)1, 20480).ToArray()).GetWebsocketBuffer(20480, CancellationToken.None);
            using (result.messageBuffer)
            {
                Assert.AreEqual(20480, result.messageBuffer.Length);
            }
            Assert.ThrowsAsync<Exception>(async () => await new FakeWebSocket(Enumerable.Repeat((byte)1, 25000).ToArray()).GetWebsocketBuffer(20480, CancellationToken.None));
        }
    }
}
