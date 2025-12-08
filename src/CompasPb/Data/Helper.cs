using System;
using System.Collections.Generic;

namespace CompasPb.Data
{
    public static class Helper
    {

        private static readonly HashSet<Type> PrimitiveTypes = new()
        {
            typeof(int),
            typeof(float),
            typeof(double),
            typeof(long),
            typeof(decimal),
            typeof(string),
            typeof(bool),
            typeof(byte),
            typeof(byte[])
        };

        public static bool IsPrimitiveType(object obj)
        {
            return obj != null && PrimitiveTypes.Contains(obj.GetType());
        }
    }
}
