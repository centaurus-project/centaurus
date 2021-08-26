using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;

namespace Centaurus.Domain
{
    public class ExtensionsManager : IDisposable
    {
        public ExtensionsManager(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
                return;

            if (!File.Exists(configFilePath))
                throw new Exception("Extensions config file is not found.");

            var extensionConfig = JsonSerializer.Deserialize<ExtensionConfig>(File.ReadAllText(configFilePath));
            var extensionsDirectory = Path.GetDirectoryName(configFilePath);
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

        public event Action<ConnectionBase> OnConnectionValidated;
        public void ConnectionReady(ConnectionBase args)
        {
            OnConnectionValidated?.Invoke(args);
        }

        public event Action<WebSocket, string> OnBeforeNewConnection;
        public void BeforeNewConnection(WebSocket args, string ip)
        {
            OnBeforeNewConnection?.Invoke(args, ip);
        }

        public event Action<ConnectionBase, MessageEnvelopeBase, Exception> OnHandleMessageFailed;
        public void HandleMessageFailed(ConnectionBase connection, MessageEnvelopeBase message, Exception exception)
        {
            OnHandleMessageFailed?.Invoke(connection, message, exception);
        }

        public event Action<ConnectionBase, MessageEnvelopeBase> OnBeforeSendMessage;
        public void BeforeSendMessage(ConnectionBase connection, MessageEnvelopeBase message)
        {
            OnBeforeSendMessage?.Invoke(connection, message);
        }

        public event Action<ConnectionBase, MessageEnvelopeBase> OnAfterSendMessage;
        public void AfterSendMessage(ConnectionBase connection, MessageEnvelopeBase message)
        {
            OnAfterSendMessage?.Invoke(connection, message);
        }

        public event Action<ConnectionBase, MessageEnvelopeBase, Exception> OnSendMessageFailed;
        public void SendMessageFailed(ConnectionBase connection, MessageEnvelopeBase message, Exception exception)
        {
            OnSendMessageFailed?.Invoke(connection, message, exception);
        }

        public event Action<ConnectionBase> OnBeforeConnectionClose;
        public void BeforeConnectionClose(ConnectionBase args)
        {
            OnBeforeConnectionClose?.Invoke(args);
        }

        public event Action<RawPubKey, MessageEnvelopeBase> OnBeforeNotify;
        public void BeforeNotify(RawPubKey pubKey, MessageEnvelopeBase envelope)
        {
            OnBeforeNotify?.Invoke(pubKey, envelope);
        }

        public event Action<MessageEnvelopeBase> OnBeforeNotifyAuditors;
        public void BeforeNotifyAuditors(MessageEnvelopeBase envelope)
        {
            OnBeforeNotifyAuditors?.Invoke(envelope);
        }

        public event Action<ConnectionBase, MessageEnvelopeBase> OnBeforeValidateMessage;
        public event Action<ConnectionBase, MessageEnvelopeBase> OnAfterValidateMessage;
        public event Action<ConnectionBase, MessageEnvelopeBase> OnBeforeHandleMessage;
        public event Action<ConnectionBase, MessageEnvelopeBase> OnAfterHandleMessage;

        public void BeforeValidateMessage(ConnectionBase connection, MessageEnvelopeBase message)
        {
            OnBeforeValidateMessage?.Invoke(connection, message);
        }

        public void AfterValidateMessage(ConnectionBase connection, MessageEnvelopeBase message)
        {
            OnAfterValidateMessage?.Invoke(connection, message);
        }

        public void BeforeHandleMessage(ConnectionBase connection, MessageEnvelopeBase message)
        {
            OnBeforeHandleMessage?.Invoke(connection, message);
        }

        public void AfterHandleMessage(ConnectionBase connection, MessageEnvelopeBase message)
        {
            OnAfterHandleMessage?.Invoke(connection, message);
        }

        public event Action<Quantum> OnBeforeQuantumHandle;
        public event Action<QuantumResultMessageBase> OnAfterQuantumHandle;
        public void BeforeQuantumHandle(Quantum quantum)
        {
            OnBeforeQuantumHandle?.Invoke(quantum);
        }

        public void AfterQuantumHandle(QuantumResultMessageBase resultMessage)
        {
            OnAfterQuantumHandle?.Invoke(resultMessage);
        }
    }
}
