using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus
{
    public static class TaskCompletionSourceExtensions
    {
        /// <summary>
        /// Sets result in separate thread to avoid deadlocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tsc"></param>
        /// <param name="result"></param>
        public static void SetResultAsync<T>(this TaskCompletionSource<T> tsc, T result)
        {
            Task.Factory.StartNew(() => tsc.SetResult(result));
        }

        /// <summary>
        /// Sets result in separate thread to avoid deadlocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tsc"></param>
        /// <param name="result"></param>
        public static void TrySetResultAsync<T>(this TaskCompletionSource<T> tsc, T result)
        {
            Task.Factory.StartNew(() => tsc.TrySetResult(result));
        }

        /// <summary>
        /// Sets exception in separate thread to avoid deadlocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tsc"></param>
        /// <param name="exc"></param>
        public static void SetExceptionAsync<T>(this TaskCompletionSource<T> tsc, Exception exc)
        {
            Task.Factory.StartNew(() => tsc.SetException(exc));
        }

        /// <summary>
        /// Sets exception in separate thread to avoid deadlocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tsc"></param>
        /// <param name="exc"></param>
        public static void TrySetExceptionAsync<T>(this TaskCompletionSource<T> tsc, Exception exc)
        {
            Task.Factory.StartNew(() => tsc.TrySetException(exc));
        }
    }
}
