using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class TaskHelper
    {
        //public static Task RunWithContext(Action action, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        //{
        //    var centaurusContext = CentaurusContext.Current;
        //    return Task.Factory.StartNew(() =>
        //    {
        //        CentaurusContext.AssignContext(centaurusContext);
        //        action();
        //    }, creationOptions);
        //}
        //public static Task<T> RunWithContext<T>(Func<T> func, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        //{
        //    var centaurusContext = CentaurusContext.Current;
        //    return Task.Factory.StartNew(() =>
        //    {
        //        CentaurusContext.AssignContext(centaurusContext);
        //        return func();
        //    }, creationOptions);
        //}
    }
}
