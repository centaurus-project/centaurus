using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentsManager: ContextualBase, IDisposable
    {
        public PaymentsManager(ExecutionContext executionContext, List<PaymentCursor> cursors, Dictionary<PaymentProvider, WithdrawalStorage> withdrawals)
            :base(executionContext)
        {
            var discoveredPaymentManagers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(PaymentsProviderBase).IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var providers = new Dictionary<PaymentProvider, PaymentsProviderBase>();
            foreach (var providerType in discoveredPaymentManagers)
            {
                var instance = (PaymentsProviderBase)Activator.CreateInstance(providerType, new object[] { Context });
                if (providers.ContainsKey(instance.Provider))
                    throw new Exception($"Payments manager for provider {instance.Provider} is already registered");

                var vault = executionContext.Constellation.Vaults.FirstOrDefault(v => v.Provider == instance.Provider);
                if (vault == null)
                    throw new Exception($"Unable to find vault for provider {instance.Provider}"); //TODO: should we throw an error or missing vault means that constellation doesn't support this payment provider

                var cursor = cursors.FirstOrDefault(c => c.Provider == instance.Provider)?.Cursor;

                withdrawals.TryGetValue(instance.Provider, out var providerWithdrawals);

                PaymentsParserManager.TryGetParser(instance.Provider, out var paymentsParser);

                //TODO: create base class for vault account and secret
                instance.Init(vault.AccountId, cursor, executionContext.Settings.Secret, paymentsParser, providerWithdrawals);
                providers.Add(instance.Provider, instance);
            }

            paymentProvider = providers.ToImmutableDictionary();
        }

        private ImmutableDictionary<PaymentProvider, PaymentsProviderBase> paymentProvider;

        public bool TryGetManager(PaymentProvider provider, out PaymentsProviderBase paymentsProvider)
        {
            return paymentProvider.TryGetValue(provider, out paymentsProvider);
        }

        public void Dispose()
        {
            foreach (var txM in paymentProvider.Values)
                txM.Dispose();
        }
    }
}
