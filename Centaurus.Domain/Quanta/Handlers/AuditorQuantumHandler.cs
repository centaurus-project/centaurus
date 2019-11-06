using Centaurus.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using NLog;
using System.Threading;

namespace Centaurus.Domain
{
    public class AuditorQuantumHandler : BaseQuantumHandler
    {
        /// <summary>
        /// Starts quanta processor thread
        /// </summary>
        protected override void StartInternal()
        {
            lock (syncRoot)
            {
                lastAddedApex = Global.QuantumStorage.CurrentApex;
            }
            Task.Factory.StartNew(RunQuantumWorker, cancellationToken.Token);
        }

        /// <summary>
        /// Validates quantum apex and puts the quanta message to queue
        /// </summary>
        /// <param name="envelope"></param>
        public override void Handle(MessageEnvelope envelope)
        {
            var quantum = envelope.Message as Quantum;
            var previousQuantumApex = quantum.Apex - 1;
            //old quantum
            if (quantum.Apex != 1 && (quantum.Apex <= Global.QuantumStorage.CurrentApex))
                return;
            //check if we have previous quantum or if it is the first quantum
            if (lastAddedApex != previousQuantumApex)
            {
                if (Global.AppState.State == ApplicationState.Ready)
                {
                    var lastApex = Math.Max(Global.QuantumStorage.CurrentApex, lastAddedApex);
                    OutgoingMessageStorage.EnqueueMessage(new SetApexCursor() { Apex = lastApex });
                    return;
                }
                else //the constellation is in a rising state
                {
                    throw new Exception("Local quanta are newer than provided by alpha");
                }
            }
            Add(envelope);
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        ulong lastAddedApex;

        Dictionary<ulong, MessageEnvelope> queue = new Dictionary<ulong, MessageEnvelope>();

        CancellationTokenSource cancellationToken = new CancellationTokenSource();

        void Add(MessageEnvelope envelope)
        {
            var quantum = envelope.Message as Quantum;
            lock (syncRoot)
            {
                if (!queue.ContainsKey(quantum.Apex))
                    queue.Add(quantum.Apex, envelope);
                else
                    queue[quantum.Apex] = envelope;

                lastAddedApex = quantum.Apex;
            }
        }

        bool TryRemove(ulong apex, out MessageEnvelope envelope)
        {
            envelope = null;
            lock (syncRoot)
            {
                if (queue.ContainsKey(apex))
                {
                    envelope = queue[apex];
                    return queue.Remove(apex);
                }
            }
            return false;
        }


        void RunQuantumWorker()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    //try to get next quantum
                    ulong targetQuantumApex = Global.QuantumStorage.CurrentApex + 1;
                    if (TryRemove(targetQuantumApex, out MessageEnvelope envelope))
                    {
                        var result = envelope.CreateResult();
                        try
                        {
                            InternalHandle(envelope);
                            result.Status = ResultStatusCodes.Success;
                            if (envelope is ITransactionContainer)
                            {
                                var transaction = ((ITransactionContainer)envelope).GetTransaction();
                                var txHash = transaction.Hash();
                                result.Effects.Add(new TransactionSignedEffect()
                                {
                                    TransactionHash = txHash,
                                    Signature = new Ed25519Signature()
                                    {
                                        Signature = Global.Settings.KeyPair.Sign(txHash),
                                        Signer = new RawPubKey { Data = Global.Settings.KeyPair.PublicKey }
                                    }
                                });
                            }

                            QuantumIsHandled(envelope);
                        }
                        catch (Exception exc)
                        {
                            logger.Error(exc);
                            result.Status = ResultStatusCodes.InternalError;

                            QuantumFailed(envelope, exc);
                        }

                        OutgoingMessageStorage.EnqueueMessage(result);
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            }
        }

        Message InternalHandle(MessageEnvelope envelope)
        {
            try
            {
                //if the quantum is a client quantum, than we need to unwrap it first
                var quantumEnvelop = envelope;
                if (envelope.Message is RequestQuantum)
                {
                    quantumEnvelop = ((RequestQuantum)envelope.Message).RequestEnvelope;
                }

                var quantum = quantumEnvelop.Message;

                if (!Global.QuantumProcessor.TryGetValue(quantum.MessageType, out IQuantumRequestProcessor processor))
                {
                    //TODO: do not fail here - return unsupported error message;
                    throw new InvalidOperationException($"Quantum {quantum.MessageType} is not supported.");
                }

                processor.Validate(envelope);

                var result = processor.Process(envelope);

                Global.QuantumStorage.AddQuantum(envelope);

                return result;
            }
            catch
            {
                logger.Info($"Handling quantum type {envelope.Message.MessageType.ToString()} failed.");
                Global.AppState.State = ApplicationState.Failed;
                throw;
            }
        }
    }
}
