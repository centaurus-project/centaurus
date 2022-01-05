using Centaurus.Models;
using System;

namespace Centaurus.Domain
{
    internal static class NodeExtensions
    {
        public static bool IsRunning(this NodeBase node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            var currentState = node.State;
            return currentState == State.Running || currentState == State.Ready || currentState == State.Chasing;
        }

        public static bool IsWaitingForInit(this NodeBase node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return node.State == State.WaitingForInit;
        }

        public static bool IsReady(this NodeBase node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return node.State == State.Ready;
        }

        public static bool IsReadyToHandleQuanta(this NodeBase node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return node.IsRunning() || node.IsWaitingForInit();
        }
    }
}
