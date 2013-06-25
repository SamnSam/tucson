﻿using System;
using System.ComponentModel;
using System.Globalization;

namespace Tucson.MongoClient  {
    /// <summary>
    /// Type Converter for <see cref="ObjectId"/>.
    /// </summary>
    /// <remarks>
    /// Currently supports conversion of a String to ObjectId
    /// </remarks>
    public class ObjectIdTypeConverter : TypeConverter {
        /// <summary>
        /// Returns whether this converter can convert an object of the given 
        /// type to the type of this converter, using the specified context.
        /// </summary>
        /// <param name="context">An 
        /// <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that 
        /// provides a format context.
        /// </param>
        /// <param name="sourceType">A <see cref="T:System.Type"/> that 
        /// represents the type you want to convert from.
        /// </param>
        /// <returns>
        /// true if this converter can perform the conversion; otherwise, false.
        /// </returns>
        public override bool CanConvertFrom(ITypeDescriptorContext context, 
            Type sourceType) {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// Converts the given object to the type of this converter, using the
        /// specified context and culture information.
        /// </summary>
        /// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/>
        /// that provides a format context.</param>
        /// <param name="culture">The <see cref="System.Globalization.CultureInfo"/>
        /// to use as the current culture.
        /// </param>
        /// <param name="value">The <see cref="object"/> to convert.</param>
        /// <returns>An <see cref="object"/> that represents the converted value.</returns>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            var valueString = value as string;
            return valueString != null ? new ObjectId(valueString) : base.ConvertFrom(context, culture, value);
        }
    }
}
