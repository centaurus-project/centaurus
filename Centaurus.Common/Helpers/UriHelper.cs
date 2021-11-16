using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace Centaurus
{
    public static class UriHelper
    {
        public static bool TryCreateUri(string address, bool useSecureConnection, out Uri uri)
        {
            return Uri.TryCreate($"{(useSecureConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp)}://{address}", UriKind.Absolute, out uri);
        }

        public static bool TryCreateUriBuilder(string address, bool useSecureConnection, out UriBuilder uriBuilder)
        {
            uriBuilder = null;
            TryCreateUri(address, useSecureConnection, out var uri);
            if (uri == null)
                return false;
            uriBuilder = new UriBuilder(uri);
            return true;
        }

        public static bool TryCreateWsConnection(string address, bool useSecureConnection, out Uri uri)
        {
            uri = null;
            if (!TryCreateUriBuilder(address, useSecureConnection, out var uriBuilder))
                return false;
            uriBuilder.Scheme = useSecureConnection ? "wss" : "ws";
            uri = uriBuilder.Uri;
            return true;
        }

        public static bool TryCreateHttpConnection(string address, bool useSecureConnection, out Uri uri)
        {
            uri = null;
            if (!TryCreateUriBuilder(address, useSecureConnection, out var uriBuilder))
                return false;
            uriBuilder.Scheme = useSecureConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            uri = uriBuilder.Uri;
            return true;
        }
    }
}
