using System;
using System.Linq.Expressions;

namespace Tucson
{
    /// <summary>
    /// The LazyPropertySetter class can improve entity usage in EntityFramework.
    /// When you set a property value in entity framework, an UPDATE statement is executed even if the property value didn't change.
    /// Eg this code causes an UPDATE even though the Property didn't change:
    ///   var oldvalue = entity.Property;
    ///   entity.Property = oldvalue;
    ///   repository.Update(entity);
    /// Instead, use SetProp to avoid the unnecessary update when you think the value may not have actually changed:
    ///   entity.SetProp(e => e.Property, oldvalue);
    /// </summary>
    public static class LazyPropertySetter
    {
        /// <summary>
        /// Lazily sets a property value to an instance only if the value has changed (same as: if (o.p != v) o.p = v)
        /// This can improve SQL performance against EF objects
        /// </summary>
        /// <typeparam name="T">The type of the instance to set</typeparam>
        /// <typeparam name="TR">An expression that returns a property from the instance</typeparam>
        /// <param name="instance">the object to change</param>
        /// <param name="getProperty">returns the property to modify</param>
        /// <param name="newvalue">the new value for the property</param>
        public static void SetProp<T, TR>(this T instance, Expression<Func<T, TR>> getProperty, TR newvalue) where T : class
        {
            var mem = getProperty.Body as MemberExpression;
            if (mem == null)
                throw new ApplicationException("getProperty method must return a property!");

            var prop = typeof(T).GetProperty(mem.Member.Name);

            var oldvalue = prop.GetValue(instance, null);

            if (!Equals(oldvalue, newvalue))
            {
                //if (typeof(T) == typeof(Applicant))
                //{
                //    var a = instance as Applicant;
                //    var appid = (a != null) ? a.ApplicantId : 0;

                //    Logger.GetLogger().Info(string.Format("Applicant:{0}, Field: {1}, Old: {2}, New: {3}", 
                //        appid,
                //        mem.Member.Name,
                //        oldvalue,
                //        newvalue));
                //}

                prop.SetValue(instance, newvalue, null);
            }
        }

        /// <summary>
        /// Gets a instance property value by name
        /// </summary>
        /// <typeparam name="T">The type of the instance</typeparam>
        /// <param name="instance">The instance</param>
        /// <param name="propName">The property name (must exist!)</param>
        /// <returns>The value of the property</returns>
        public static object GetProp<T>(this T instance, string propName) where T : class
        {
            var prop = instance.GetType().GetProperty(propName);

            if (prop == null)
                throw new ArgumentOutOfRangeException("propName", "Property " + propName + " not found on type " + instance.GetType().FullName);

            return prop.GetValue(instance, null);
        }
    }
}
