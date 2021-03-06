﻿using System;
using System.Data;

namespace Olive.Entities.ObjectDataProvider
{
    static class NullSafeGetter
    {
        public static T GetValueOrDefault<T>(this IDataRecord @this, QueryColumn queryColumn)
        {
            var ordinal = @this.GetOrdinal(queryColumn.Identifier);
            return @this.GetValueOrDefault<T>(ordinal);
        }

        public static T GetValueOrDefault<T>(this IDataRecord @this, int ordinal)
        {
            if (@this.IsDBNull(ordinal))
                return default(T);
            else
            {
                var temp = @this.GetValue(ordinal);
                return (T)Convert.ChangeType(temp, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)); ;
            }
        }
    }
}