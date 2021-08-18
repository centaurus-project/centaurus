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
        public ExtensionsManager(string extensionsConfigFilePath)
        {
            if (string.IsNullOrWhiteSpace(extensionsConfigFilePath))
                return;

            var configFilePath = Path.GetFullPath(extensionsConfigFilePath);
            if (!File.Exists(configFilePath))
                throw new Exception("Extensions config file is not found.");

            var extensionConfig = JsonSerializer.Deserialize<ExtensionConfig>(File.ReadAllText(configFilePath));
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

        public event Action<ConnectionBase, MessageEnvelope, Exception> OnHandleMessageFailed;
        public void HandleMessageFailed(ConnectionBase connection, MessageEnvelope message, Exception exception)
        {
            OnHandleMessageFailed?.Invoke(connection, message, exception);
        }

        public event Action<ConnectionBase, MessageEnvelope> OnBeforeSendMessage;
        public void BeforeSendMessage(ConnectionBase connection, MessageEnvelope message)
        {
            OnBeforeSendMessage?.Invoke(connection, message);
        }

        public event Action<ConnectionBase, MessageEnvelope> OnAfterSendMessage;
        public void AfterSendMessage(ConnectionBase connection, MessageEnvelope message)
        {
            OnAfterSendMessage?.Invoke(connection, message);
        }

        public event Action<ConnectionBase, MessageEnvelope, Exception> OnSendMessageFailed;
        public void SendMessageFailed(ConnectionBase connection, MessageEnvelope message, Exception exception)
        {
            OnSendMessageFailed?.Invoke(connection, message, exception);
        }

        public event Action<ConnectionBase> OnBeforeConnectionClose;
        public void BeforeConnectionClose(ConnectionBase args)
        {
            OnBeforeConnectionClose?.Invoke(args);
        }

        public event Action<RawPubKey, MessageEnvelope> OnBeforeNotify;
        public void BeforeNotify(RawPubKey pubKey, MessageEnvelope envelope)
        {
            OnBeforeNotify?.Invoke(pubKey, envelope);
        }

        public event Action<MessageEnvelope> OnBeforeNotifyAuditors;
        public void BeforeNotifyAuditors(MessageEnvelope envelope)
        {
            OnBeforeNotifyAuditors?.Invoke(envelope);
        }

        public event Action<ConnectionBase, MessageEnvelope> OnBeforeValidateMessage;
        public event Action<ConnectionBase, MessageEnvelope> OnAfterValidateMessage;
        public event Action<ConnectionBase, MessageEnvelope> OnBeforeHandleMessage;
        public event Action<ConnectionBase, MessageEnvelope> OnAfterHandleMessage;

        public void BeforeValidateMessage(ConnectionBase connection, MessageEnvelope message)
        {
            OnBeforeValidateMessage?.Invoke(connection, message);
        }

        public void AfterValidateMessage(ConnectionBase connection, MessageEnvelope message)
        {
            OnAfterValidateMessage?.Invoke(connection, message);
        }

        public void BeforeHandleMessage(ConnectionBase connection, MessageEnvelope message)
        {
            OnBeforeHandleMessage?.Invoke(connection, message);
        }

        public void AfterHandleMessage(ConnectionBase connection, MessageEnvelope message)
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
