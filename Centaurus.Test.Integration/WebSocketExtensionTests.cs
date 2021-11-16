using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Xdr;

namespace Centaurus.Test
{
    [TestFixture]
    public class WebSocketExtensionTests
    {
        [Test]
        public async Task GetInputStreamReaderTest()
        {
            using var buffer = XdrBufferFactory.Rent(20480);
            var result = await new DummyWebSocket(Enumerable.Repeat((byte)1, 10240).ToArray()).GetWebsocketBuffer(buffer, CancellationToken.None);
            Assert.AreEqual(10240, buffer.Length);
            result = await new DummyWebSocket(Enumerable.Repeat((byte)1, 20480).ToArray()).GetWebsocketBuffer(buffer, CancellationToken.None);
            Assert.AreEqual(20480, buffer.Length);
            Assert.ThrowsAsync<Exception>(async () => await new DummyWebSocket(Enumerable.Repeat((byte)1, 25000).ToArray()).GetWebsocketBuffer(buffer, CancellationToken.None));
        }
    }
}
