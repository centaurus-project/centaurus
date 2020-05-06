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
    public class EnvelopeEventArgs
    {
        public BaseWebSocketConnection Connection { get; set; }

        public MessageEnvelope Message { get; set; }

    }

    public class EnvelopeErrorEventArgs : EnvelopeEventArgs
    {
        public Exception Exception { get; set; }
    }

    public class NotifyEventArgs
    {
        public RawPubKey Account { get; set; }
        public MessageEnvelope Envelope { get; set; }
    }

    public class ExtensionsManager
    {
        public async Task RegisterAllExtensions()
        {
            if (string.IsNullOrWhiteSpace(Global.Settings.ExtensionsConfigFilePath))
                return;

            var configFilePath = Path.GetFullPath(Global.Settings.ExtensionsConfigFilePath);
            if (!File.Exists(configFilePath))
                throw new Exception("Extensions config file is not found.");

            var extensionConfig = JsonConvert.DeserializeObject<ExtensionConfig>(File.ReadAllText(configFilePath));

            foreach (var configItem in extensionConfig.Extensions)
            {
                var extension = ExtensionItem.Load(configItem);
                await extension.ExtensionInstance.Init(configItem.ExtensionConfig);

                extensions.Add(extension);
            }
        }

        private List<ExtensionItem> extensions = new List<ExtensionItem>();

        public IEnumerable<ExtensionItem> Extensions => extensions;

        public event EventHandler<WebSocket> OnBeforeNewConnection;
        public event EventHandler<BaseWebSocketConnection> OnConnectionValidated;
        public void ConnectionValidated(BaseWebSocketConnection args)
        {
            OnConnectionValidated?.Invoke(this, args);
        }
        public void BeforeNewConnection(WebSocket args)
        {
            OnBeforeNewConnection?.Invoke(this, args);
        }

        public event EventHandler<EnvelopeErrorEventArgs> OnHandleMessageFailed;
        public void HandleMessageFailed(EnvelopeErrorEventArgs args)
        {
            OnHandleMessageFailed?.Invoke(this, args);
        }

        public event EventHandler<EnvelopeEventArgs> OnBeforeSendMessage;
        public event EventHandler<EnvelopeEventArgs> OnAfterSendMessage;
        public event EventHandler<EnvelopeErrorEventArgs> OnSendMessageFailed;
        public void BeforeSendMessage(EnvelopeEventArgs args)
        {
            OnBeforeSendMessage?.Invoke(this, args);
        }
        public void AfterSendMessage(EnvelopeEventArgs args)
        {
            OnAfterSendMessage?.Invoke(this, args);
        }
        public void SendMessageFailed(EnvelopeErrorEventArgs args)
        {
            OnSendMessageFailed?.Invoke(this, args);
        }

        public event EventHandler<BaseWebSocketConnection> OnBeforeConnectionClose;

        public void BeforeConnectionClose(BaseWebSocketConnection args)
        {
            OnBeforeConnectionClose?.Invoke(this, args);
        }

        public event EventHandler<NotifyEventArgs> OnBeforeNotify;
        public event EventHandler<MessageEnvelope> OnBeforeNotifyAuditors;


        public void BeforeNotify(NotifyEventArgs args)
        {
            OnBeforeNotify?.Invoke(this, args);
        }

        public void BeforeNotifyAuditors(MessageEnvelope args)
        {
            OnBeforeNotifyAuditors?.Invoke(this, args);
        }

        public event EventHandler<EnvelopeEventArgs> OnBeforeValidateMessage;
        public event EventHandler<EnvelopeEventArgs> OnAfterValidateMessage;
        public event EventHandler<EnvelopeEventArgs> OnBeforeHandleMessage;
        public event EventHandler<EnvelopeEventArgs> OnAfterHandleMessage;

        public void BeforeValidateMessage(EnvelopeEventArgs args)
        {
            OnBeforeValidateMessage?.Invoke(this, args);
        }

        public void AfterValidateMessage(EnvelopeEventArgs args)
        {
            OnAfterValidateMessage?.Invoke(this, args);
        }

        public void BeforeHandleMessage(EnvelopeEventArgs args)
        {
            OnBeforeHandleMessage?.Invoke(this, args);
        }

        public void AfterHandleMessage(EnvelopeEventArgs args)
        {
            OnAfterHandleMessage?.Invoke(this, args);
        }

        public event EventHandler<MessageEnvelope> OnBeforeQuantumHandle;
        public event EventHandler<ResultMessage> OnAfterQuantumHandle;
        public void BeforeQuantumHandle(MessageEnvelope args)
        {
            OnBeforeQuantumHandle?.Invoke(this, args);
        }

        public void AfterQuantumHandle(ResultMessage args)
        {
            OnAfterQuantumHandle?.Invoke(this, args);
        }
    }
}
