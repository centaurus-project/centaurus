using Centaurus.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ExtensionsManager : IDisposable
    {
        public ExtensionsManager(string extensionsConfigFilePath)
        {
            if (string.IsNullOrWhiteSpace(extensionsConfigFilePath))
                return;

            var configFilePath = Path.GetFullPath(extensionsConfigFilePath);
            if (!File.Exists(configFilePath))
                throw new Exception("Extensions config file is not found.");

            var extensionConfig = JsonConvert.DeserializeObject<ExtensionConfig>(File.ReadAllText(configFilePath));
            var extensionsDirectory = Path.GetDirectoryName(extensionsConfigFilePath);
            foreach (var configItem in extensionConfig.Extensions)
            {
                var extension = ExtensionItem.Load(configItem, extensionsDirectory);
                extension.ExtensionInstance.Init(configItem.ExtensionConfig);

                extensions.Add(extension);
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
                return;
                foreach (var extension in extensions)
                {
                    extension.ExtensionInstance.Dispose();
                }
            isDisposed = true;
        }

        private List<ExtensionItem> extensions = new List<ExtensionItem>();
        private bool isDisposed;

        public IEnumerable<ExtensionItem> Extensions => extensions;


        public event Action<WebSocket, string> OnBeforeNewConnection;
        public event Action<BaseWebSocketConnection> OnConnectionValidated;

        public void ConnectionValidated(BaseWebSocketConnection args)
        {
            OnConnectionValidated?.Invoke(args);
        }
        public void BeforeNewConnection(WebSocket args, string ip)
        {
            OnBeforeNewConnection?.Invoke(args, ip);
        }

        public event Action<BaseWebSocketConnection, MessageEnvelope, Exception> OnHandleMessageFailed;
        public void HandleMessageFailed(BaseWebSocketConnection connection, MessageEnvelope message, Exception exception)
        {
            OnHandleMessageFailed?.Invoke(connection, message, exception);
        }

        public event Action<BaseWebSocketConnection, MessageEnvelope> OnBeforeSendMessage;
        public event Action<BaseWebSocketConnection, MessageEnvelope> OnAfterSendMessage;
        public event Action<BaseWebSocketConnection, MessageEnvelope, Exception> OnSendMessageFailed;
        public void BeforeSendMessage(BaseWebSocketConnection connection, MessageEnvelope message)
        {
            OnBeforeSendMessage?.Invoke(connection, message);
        }
        public void AfterSendMessage(BaseWebSocketConnection connection, MessageEnvelope message)
        {
            OnAfterSendMessage?.Invoke(connection, message);
        }
        public void SendMessageFailed(BaseWebSocketConnection connection, MessageEnvelope message, Exception exception)
        {
            OnSendMessageFailed?.Invoke(connection, message, exception);
        }

        public event Action<BaseWebSocketConnection> OnBeforeConnectionClose;

        public void BeforeConnectionClose(BaseWebSocketConnection args)
        {
            OnBeforeConnectionClose?.Invoke(args);
        }

        public event Action<RawPubKey, MessageEnvelope> OnBeforeNotify;
        public event Action<MessageEnvelope> OnBeforeNotifyAuditors;
        public void BeforeNotify(RawPubKey pubKey, MessageEnvelope envelope)
        {
            OnBeforeNotify?.Invoke(pubKey, envelope);
        }

        public void BeforeNotifyAuditors(MessageEnvelope envelope)
        {
            OnBeforeNotifyAuditors?.Invoke(envelope);
        }

        public event Action<BaseWebSocketConnection, MessageEnvelope> OnBeforeValidateMessage;
        public event Action<BaseWebSocketConnection, MessageEnvelope> OnAfterValidateMessage;
        public event Action<BaseWebSocketConnection, MessageEnvelope> OnBeforeHandleMessage;
        public event Action<BaseWebSocketConnection, MessageEnvelope> OnAfterHandleMessage;

        public void BeforeValidateMessage(BaseWebSocketConnection connection, MessageEnvelope message)
        {
            OnBeforeValidateMessage?.Invoke(connection, message);
        }

        public void AfterValidateMessage(BaseWebSocketConnection connection, MessageEnvelope message)
        {
            OnAfterValidateMessage?.Invoke(connection, message);
        }

        public void BeforeHandleMessage(BaseWebSocketConnection connection, MessageEnvelope message)
        {
            OnBeforeHandleMessage?.Invoke(connection, message);
        }

        public void AfterHandleMessage(BaseWebSocketConnection connection, MessageEnvelope message)
        {
            OnAfterHandleMessage?.Invoke(connection, message);
        }

        public event Action<MessageEnvelope> OnBeforeQuantumHandle;
        public event Action<ResultMessage> OnAfterQuantumHandle;
        public void BeforeQuantumHandle(MessageEnvelope envelope)
        {
            OnBeforeQuantumHandle?.Invoke(envelope);
        }

        public void AfterQuantumHandle(ResultMessage resultMessage)
        {
            OnAfterQuantumHandle?.Invoke(resultMessage);
        }
    }
}
