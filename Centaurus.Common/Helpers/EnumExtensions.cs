using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus
{
    public static class EnumExtensions
    {
        public static T[] GetValues<T>()
            where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        }

        /// <summary>
        /// Gets values of specified enum and casts it to target type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TCastTarget"></typeparam>
        /// <returns></returns>
        public static TCastTarget[] GetValues<T, TCastTarget>()
            where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<TCastTarget>().ToArray();
        }    
    }
}
