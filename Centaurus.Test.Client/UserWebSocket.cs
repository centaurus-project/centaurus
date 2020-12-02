using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Test.Client
{
    public class UserWebSocketConnection : BaseWebSocketConnection
    {
        public UserWebSocketConnection(string ip)
        : base(new ClientWebSocket(), ip)
        {
            //we don't need to create and sign heartbeat message on every sending
            hearbeatMessage = new Heartbeat().CreateEnvelope();
            hearbeatMessage.Sign(Global.Settings.KeyPair);
#if !DEBUG
            InitTimer();
#endif
        }

        private System.Timers.Timer heartbeatTimer = null;

        //If we didn't receive message during specified interval, we should close connection
        private void InitTimer()
        {
            heartbeatTimer = new System.Timers.Timer();
            heartbeatTimer.Interval = 5000;
            heartbeatTimer.AutoReset = false;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
        }

        private MessageEnvelope hearbeatMessage;

        private async void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await SendMessage(hearbeatMessage);
        }

        protected ClientWebSocket clientWebSocket => webSocket as ClientWebSocket;

        public virtual async Task EstablishConnection()
        {
            await (webSocket as ClientWebSocket).ConnectAsync(new Uri(((AuditorSettings)Global.Settings).AlphaAddress), CancellationToken.None);
            _ = Listen();
        }

        public override async Task SendMessage(MessageEnvelope envelope, CancellationToken ct = default)
        {
            await base.SendMessage(envelope, ct);
            if (heartbeatTimer != null)
                heartbeatTimer.Reset();
        }


        static Logger logger = LogManager.GetCurrentClassLogger();

        public ConcurrentDictionary<long, TaskCompletionSource<MessageEnvelope>> Requests { get; } = new ConcurrentDictionary<long, TaskCompletionSource<MessageEnvelope>>();

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            return await MessageHandlers.HandleMessage(this, envelope);
        }

        /// <summary>
        /// Method to break encapsulation
        /// </summary>
        /// <param name="state">State to set</param>
        public void SetConnectionState(ConnectionState state)
        {
            ConnectionState = state;
        }

        public async Task<MessageEnvelope> SendMessage(Message message)
        {
            await base.SendMessage(message);
            var messageId = message.MessageId;
            //add the handler to the awaitable queue if the clients expects a response for that kind of messages
            if (messageId > 0)
            {
                var response = new TaskCompletionSource<MessageEnvelope>(new { createdAt = DateTime.UtcNow.Ticks });
                if (Requests.TryAdd(messageId, response))
                {//return response task
                    return await response.Task;
                }
            }
            return await Task.FromResult<MessageEnvelope>(null);
        }

        public async Task<ResultMessage> PlaceOrder(int side, long amount, double price)
        {
            try
            {
                var order = new OrderRequest
                {
                    Asset = 1,
                    Amount = amount,
                    Nonce = DateTime.Now.Ticks,
                    Account = new RawPubKey() { Data = Global.Settings.KeyPair.PublicKey },
                    Price = price,
                    Side = (OrderSide)side
                };

                var response = await SendMessage(order);
                var result = response?.Message as ResultMessage;
                return result;
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
                throw exc;
            }
        }
    }
}
