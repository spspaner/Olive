﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Olive
{
    partial class OliveExtensions
    {
        /// <summary>
        /// Shortens this GUID.
        /// </summary>
        public static ShortGuid Shorten(this Guid @this) => new ShortGuid(@this);

        public static bool IsAnyOf(this Guid? @this, params Guid?[] items)
            => items?.Contains(@this) ?? false;

        public static bool IsAnyOf(this Guid? @this, IEnumerable<Guid?> items)
            => items?.Contains(@this) ?? false;

        public static bool IsAnyOf(this Guid? @this, IEnumerable<Guid> items)
            => @this.HasValue && (items?.Contains(@this) == true);

        public static bool IsAnyOf(this Guid @this, params Guid?[] items)
            => items?.Contains(@this) ?? false;

        public static bool IsAnyOf(this Guid @this, IEnumerable<Guid?> items)
            => items?.Contains(@this) ?? false;

        public static bool IsAnyOf(this Guid @this, IEnumerable<Guid> items)
            => items?.Contains(@this) ?? false;
    }
}
