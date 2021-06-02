using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models.Extensions
{
    public static class VaultExtensions
    {
        public static bool TryGetVault(this List<Vault> vaults, PaymentProvider provider, out Vault vault)
        {
            if (vaults == null)
                throw new ArgumentNullException(nameof(vaults));

            vault = vaults.FirstOrDefault(v => v.Provider == provider);
            return vault != null;
        }

        public static Vault GetVault(this List<Vault> vaults, PaymentProvider provider)
        {
            if (!vaults.TryGetVault(provider, out var vault))
                throw new Exception($"Unable to find vault for provider {provider}.");
            return vault;
        }
    }
}
