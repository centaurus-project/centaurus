using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Stellar.PaymentProvider
{
    public class StellarPaymentProvider : PaymentProviderBase
    {
        public StellarPaymentProvider(SettingsModel settings, string config)
            : base(settings, config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var configObject = JsonSerializer.Deserialize<Config>(config);

            Network = new Network(configObject.PassPhrase);

            Network.Use(Network);

            Server = new Server(configObject.Horizon);

            secret = KeyPair.FromSecretSeed(configObject.Secret);

            //load account sequence
            LoadVaultAccountData().Wait();

            //wait while all missed transactions will be loaded
            LoadLastTxs().Wait();

            Task.Factory.StartNew(ListenTransactions, TaskCreationOptions.LongRunning);
        }

        public override bool IsTransactionValid(byte[] rawTransaction, WithdrawalRequestModel withdrawalRequest, out string error)
        {
            error = null;
            if (rawTransaction == null)
                throw new ArgumentNullException(nameof(rawTransaction));

            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            if (!rawTransaction.AsSpan().SequenceEqual(BuildTransaction(withdrawalRequest)))
            {
                error = "Transaction is not equal to expected one.";
                return false;
            }
            return true;
        }

        public override byte[] BuildTransaction(WithdrawalRequestModel withdrawalRequest)
        {
            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            if (!ValidateFee((uint)withdrawalRequest.Fee))
                throw new InvalidOperationException($"Not fair fee {withdrawalRequest.Fee}.");

            var sourceAccount = new Account(Vault, vaultAccount.SequenceNumber);
            var options = new TransactionBuilderOptions(sourceAccount, (uint)withdrawalRequest.Fee);
            if (!Settings.TryGetAsset(withdrawalRequest.Asset, out var stellarAsset))
                throw new InvalidOperationException($"Asset {withdrawalRequest.Asset} is not supported by provider.");

            var transaction = TransactionHelper.BuildPaymentTransaction(options, KeyPair.FromPublicKey(withdrawalRequest.Destination), stellarAsset, (long)withdrawalRequest.Amount);

            var stream = new XdrDataOutputStream();
            stellar_dotnet_sdk.xdr.Transaction.Encode(stream, transaction.ToXdrV1());
            var rawTx = stream.ToArray();
            //inc sequence
            vaultAccount.SequenceNumber++;
            return rawTx;
        }

        public override SignatureModel SignTransaction(byte[] transaction)
        {
            if (!StellarTransactionExtensions.TryDeserializeTransaction(transaction, out var tx))
                throw new Exception("Unable to deserialize transaction.");

            if (tx.Signatures.Count > 0)
                throw new ArgumentException("Transaction contains signatures.");

            tx.Sign(secret, Network);
            var signature = tx.Signatures.First();
            return new SignatureModel { Signer = secret.PublicKey, Signature = signature.Signature.InnerValue };
        }

        public override async Task<bool> SubmitTransaction(byte[] transaction, List<SignatureModel> signatures)
        {
            if (!StellarTransactionExtensions.TryDeserializeTransaction(transaction, out var tx))
                throw new Exception("Unable to deserialize transaction.");

            if (tx.Signatures.Count > 0)
                throw new ArgumentException("Transaction contains signatures.");

            if (signatures.Count < 1)
                throw new ArgumentException("Signatures collection is empty.");

            AddSignatures(tx, signatures);

            var txItem = new TransactionItem { Transaction = tx };

            _ = Task.Factory.StartNew(() => TrySubmitTransaction(txItem));

            return await txItem.OnSubmitted.Task;
        }

        public override int CompareCursors(string left, string right)
        {
            if (!(long.TryParse(left, out var leftLong) && long.TryParse(right, out var rightLong)))
                throw new Exception("Unable to convert cursor to long.");
            return Comparer<long>.Default.Compare(leftLong, rightLong);
        }

        private async Task LoadVaultAccountData()
        {
            vaultAccount = await Server.Accounts.Account(Vault);
            lastSubmitedSequence = GetLastSubmittedSequence().Result;
            if (lastSubmitedSequence == default)
                lastSubmitedSequence = vaultAccount.SequenceNumber;
        }

        private AccountResponse vaultAccount;

        private long lastSubmitedSequence;

        private async Task<long> GetLastSubmittedSequence()
        {
            var pageSize = 200;
            var transactionResponse = await Server
                .GetTransactionsRequestBuilder(Vault, 0, pageSize, true, true)
                .Execute();
            while (transactionResponse.Records.Count > 0)
            {
                foreach (var tx in transactionResponse.Records)
                {
                    var transaction = stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(tx.EnvelopeXdr);
                    if (transaction.SourceAccount.AccountId != Vault)
                        continue;
                    return transaction.SequenceNumber;
                }
                transactionResponse = await transactionResponse.NextPage();
            }
            return 0;
        }

        private async Task LoadLastTxs()
        {
            var pageSize = 200;
            var transactionResponse = await Server
                .GetTransactionsRequestBuilder(Vault, long.Parse(LastRegisteredCursor), pageSize, true)
                .Execute();

            while (transactionResponse.Records.Count > 0)
            {
                foreach (var tx in transactionResponse.Records)
                {
                    ProcessTransaction(tx);
                }
                transactionResponse = await transactionResponse.NextPage();
            }
        }

        private bool ValidateFee(uint fee)
        {
            return true;
        }

        private SortedDictionary<long, TransactionItem> transactions = new SortedDictionary<long, TransactionItem>();

        private void AddSignatures(stellar_dotnet_sdk.Transaction tx, List<SignatureModel> signatures)
        {
            var currentWeight = 0;
            foreach (var signature in signatures)
            {
                var signerKey = KeyPair.FromPublicKey(signature.Signer);
                var currentSigner = vaultAccount.Signers.FirstOrDefault(s => s.Key == signerKey.AccountId);
                if (currentSigner == null)
                {
                    RaiseOnError(new Exception($"Unknown signer {signerKey.AccountId}"));
                    continue;
                }
                tx.Signatures.Add(new DecoratedSignature { Hint = signerKey.SignatureHint, Signature = new Signature(signature.Signature) });
                currentWeight += currentSigner.Weight;
                if (currentWeight >= vaultAccount.Thresholds.MedThreshold)
                    break;
            }

            if (currentWeight < vaultAccount.Thresholds.MedThreshold)
                throw new Exception("Unable to reach required threshold weight.");
        }

        SemaphoreSlim txSubmitSemaphore = new SemaphoreSlim(1);
        private Network Network;

        private async Task TrySubmitTransaction(TransactionItem transactionItem)
        {
            await txSubmitSemaphore.WaitAsync();
            try
            {
                transactions.Add(transactionItem.Transaction.SequenceNumber, transactionItem);
                while (transactions.Count > 0)
                {
                    transactionItem = transactions.Values.First();
                    try
                    {
                        var txSequence = transactionItem.Transaction.SequenceNumber;
                        var expectedSequence = lastSubmitedSequence + 1;
                        if (transactionItem.Transaction.SequenceNumber > expectedSequence)
                            //wait for previous tx
                            break;
                        else if (txSequence == expectedSequence)
                        {
                            lastSubmitedSequence = expectedSequence;
                            var result = await Server.SubmitTransaction(transactionItem.Transaction);
                            var isSuccess = result.IsSuccess();
                            transactionItem.OnSubmitted.SetResult(isSuccess);
                            if (!isSuccess)
                                RaiseOnError(new Exception($"Transaction with sequence {transactionItem.Transaction.SequenceNumber} submit failed. {result.ResultXdr}"));
                        }
                        else if (transactionItem.Transaction.SequenceNumber < expectedSequence)
                        {
                            transactionItem.OnSubmitted.SetResult(false);
                        }
                        transactions.Remove(transactionItem.Transaction.SequenceNumber);
                    }
                    catch (Exception exc)
                    {
                        transactionItem.OnSubmitted.TrySetException(exc);
                        RaiseOnError(exc);
                    }
                }
            }
            finally
            {
                txSubmitSemaphore.Release();
            }
        }

        List<DepositModel> GetVaultPayments(stellar_dotnet_sdk.Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<DepositModel>();
            var txHash = transaction.Hash();

            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var destination = (transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey).PublicKey;
                if (Settings.TryGetDeposit(transaction.Operations[i].ToOperationBody(), destination, isSuccess, txHash, out DepositModel payment))
                    ledgerPayments.Add(payment);
            }
            return ledgerPayments;
        }

        void ProcessTransaction(TransactionResponse tx)
        {
            try
            {
                var payments = GetVaultPayments(stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.Successful);
                var payment = new DepositNotificationModel
                {
                    ProviderId = Id,
                    Cursor = tx.PagingToken.ToString(),
                    Items = payments,
                    DepositTime = DateTime.UtcNow
                };

                NotificationsManager.RegisterNotification(payment);
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();

                //if worker is broken, the auditor should quit consensus
                RaiseOnError(e);
                return;
            }
        }

        async Task ListenTransactions()
        {
            var failedDates = new List<DateTime>();
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (failedDates.Count > 0)
                    await Task.Delay(new TimeSpan(0, 1, 0));

                var listener = default(IEventSource);
                try
                {
                    listener = Server
                        .GetTransactionsRequestBuilder(Vault, long.Parse(LastRegisteredCursor), includeFailed: true)
                        .Stream((_, tx) => ProcessTransaction(tx));

                    await listener.Connect();
                }
                catch (Exception exc)
                {
                    //dispose current listener instance
                    listener?.Shutdown();
                    listener?.Dispose();

                    //clear if last fail was long ago
                    if (failedDates.Count > 0 && DateTime.UtcNow - failedDates.LastOrDefault() > new TimeSpan(0, 10, 0))
                        failedDates.Clear();
                    failedDates.Add(DateTime.UtcNow);
                    if (failedDates.Count > 5)
                    {
                        var e = exc;
                        if (exc is AggregateException)
                            e = exc.GetBaseException();

                        RaiseOnError(exc);
                        return;
                    }
                }
            }
        }

        private Server Server;
        private readonly KeyPair secret;

        public override bool AreSignaturesValid(byte[] transaction, params SignatureModel[] signatures)
        {
            if (signatures.Length < 1)
                throw new ArgumentException("At least one signature must be specified.");

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                var decoratedSignatures = new List<DecoratedSignature>();
                foreach (var signature in signatures)
                {
                    var pubKey = KeyPair.FromPublicKey(signature.Signer);
                    if (!pubKey.Verify(transaction, signature.Signature))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        class TransactionItem
        {
            public stellar_dotnet_sdk.Transaction Transaction { get; set; }

            public TaskCompletionSource<bool> OnSubmitted { get; } = new TaskCompletionSource<bool>();
        }
    }

    class Config
    {
        public string Horizon { get; set; }

        public string Secret { get; set; }

        public string PassPhrase { get; set; }
    }
}