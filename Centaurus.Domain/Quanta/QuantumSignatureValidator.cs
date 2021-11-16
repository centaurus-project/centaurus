using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class QuantumSignatureValidator
    {
        static QuantumSignatureValidator()
        {
            Task.Factory.StartNew(StartVerifications, TaskCreationOptions.LongRunning);
        }

        public static Task<bool> Validate(Quantum quantum)
        {
            if (quantum is RequestQuantumBase request)
            {
                var verificationTask = new TaskCompletionSource<bool>();
                verificationTasks.Add(() => VerifyQuantumSignature(verificationTask, request));
                return verificationTask.Task;
            }

            return Task.FromResult(true);
        }

        private static void StartVerifications()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 5 };
            var partitioner = Partitioner.Create(verificationTasks.GetConsumingEnumerable(), EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(partitioner, options, verify => verify());
        }

        private static void VerifyQuantumSignature(TaskCompletionSource<bool> verificationTask, RequestQuantumBase request)
        {
            try
            {
                verificationTask.SetResult(request.RequestEnvelope.IsSignatureValid(request.RequestMessage.Account));
            }
            catch (Exception exc)
            {
                verificationTask.SetException(exc);
            }
        }

        private static BlockingCollection<Action> verificationTasks = new BlockingCollection<Action>();

    }
}
