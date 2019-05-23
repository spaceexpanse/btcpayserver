using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BTCPayServer
{
    public class ReflectionExtensions
    {
        public static List<T> GetInstancesImplementingInterface<T>()
        {
            return (from t in Assembly.GetExecutingAssembly().GetTypes()
                where t.GetInterfaces().Contains(typeof (T)) && t.GetConstructor(Type.EmptyTypes) != null
                select (T) Activator.CreateInstance(t)).ToList();
        }
        
        public static IList<T> GetInstancesExtendingType<T>()
        {
            return (from t in Assembly.GetExecutingAssembly().GetTypes()
                where t.BaseType == (typeof(T)) && t.GetConstructor(Type.EmptyTypes) != null
                select (T)Activator.CreateInstance(t)).ToList();
        }
    }
}
