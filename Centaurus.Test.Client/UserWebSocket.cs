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
    public class UserWebSocketConnection : ClientWebSocketConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public ConcurrentDictionary<ulong, TaskCompletionSource<MessageEnvelope>> Requests { get; } = new ConcurrentDictionary<ulong, TaskCompletionSource<MessageEnvelope>>();

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
                    Nonce = (ulong)DateTime.Now.Ticks,
                    Account = new RawPubKey() { Data = Global.Settings.KeyPair.PublicKey },
                    Price = price,
                    Side = (OrderSides)side
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
