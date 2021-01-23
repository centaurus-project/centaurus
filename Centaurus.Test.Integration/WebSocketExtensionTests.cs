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
            using (var res = await new FakeWebSocket(Enumerable.Repeat((byte)1, 10240).ToArray()).GetWebsocketBuffer(CancellationToken.None))
            {
                Assert.AreEqual(10240, res.Length);
            }
            using (var res = await new FakeWebSocket(Enumerable.Repeat((byte)1, 20480).ToArray()).GetWebsocketBuffer(CancellationToken.None))
            {
                Assert.AreEqual(20480, res.Length);
            }
            Assert.ThrowsAsync<Exception>(async () => await new FakeWebSocket(Enumerable.Repeat((byte)1, 25000).ToArray()).GetWebsocketBuffer(CancellationToken.None));
        }
    }
}
