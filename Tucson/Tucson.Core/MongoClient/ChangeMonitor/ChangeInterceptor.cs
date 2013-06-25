using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Castle.DynamicProxy;

namespace Tucson.MongoClient.ChangeMonitor
{
    internal abstract class ChangeInterceptor : DisposableBase, IInterceptor, IMongoModelChangeMonitor
    {
        public object ProxyInstance { get; private set; }
        public List<IMongoModelChilldPropertyChangeMonitor> ChildChangeMonitors { get; set; }

        protected Type InstanceType { get; private set; }
        protected object Instance { get; private set; }


        ~ChangeInterceptor()
        {
            Dispose(false);
        }

        public virtual void ClearChanges()
        {
            if (ChildChangeMonitors != null)
            {
                foreach (var child in ChildChangeMonitors)
                    child.Monitor.ClearChanges();
            }
        }

        public abstract bool HasChanges { get; }

        public virtual void SetProxyInstance(object instance, Type instanceType, object proxyInstance)
        {
            Instance = instance;
            InstanceType = instanceType;
            ProxyInstance = proxyInstance;
        }

        /// <summary>
        /// Sets the UpdateBuilder with changes made
        /// </summary>
        /// <param name="parentName">name of the parent element</param>
        /// <param name="updateBuilder">the mongo update builder</param>
        public abstract void SetChanges(string parentName, MongoDB.Driver.Builders.UpdateBuilder updateBuilder);

        public abstract void Intercept(IInvocation invocation);

        /// <summary>
        /// returns true if the type is wrappable and the value is wrappable (stupid, I know)
        /// </summary>
        /// <param name="t"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        protected static bool IsWrappableObject(Type t, object v)
        {
            return IsProxyableType(t) && !(v is ICollection);
        }

        protected static bool IsProxyableType(Type t)
        {
            return t.IsClass && !typeof(string).IsAssignableFrom(t);
        }

        protected string GetMongoName(string parentName, string field)
        {
            var en = MongoMapFactory.GetMongoElementName(InstanceType, field);
            var fn = parentName == null
                         ? en
                         : string.Format("{0}.{1}", parentName, en);

            return fn;
        }

        protected enum ValueResultEnum
        {
            Changed,
            NotChanged
        }

        protected static ValueResultEnum IsValueEqual(object oldvalue, object newvalue)
        {
            if (Equals(oldvalue, newvalue))
                return ValueResultEnum.NotChanged;

            if (newvalue != null && oldvalue != null)
            {
                // both old and new values are defined, so let's check if they're wrappable/lists
                if (IsWrappableObject(newvalue.GetType(), newvalue))
                {
                    // set class object variables
                    SetNestedClassProperties(oldvalue, newvalue);
                    return ValueResultEnum.NotChanged;
                }

                if (newvalue is IList && oldvalue is IList)
                    // set list properties
                    return SetNestedListProperties((IList) oldvalue, (IList) newvalue);
            }

            return ValueResultEnum.Changed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldlist"></param>
        /// <param name="newlist"></param>
        /// <returns>True if the property should be set</returns>
        protected static ValueResultEnum SetNestedListProperties(IList oldlist, IList newlist)
        {
            if (oldlist.Count != newlist.Count)
                return ValueResultEnum.Changed; // list are really different

            for (var idx = 0; idx < oldlist.Count; idx++)
            {
                var oldvalue = oldlist[idx];
                var newvalue = newlist[idx];

                if (IsValueEqual(oldvalue, newvalue) == ValueResultEnum.Changed)
                    oldlist[idx] = newvalue;
            }

            return ValueResultEnum.NotChanged;
        }


        /// <summary>
        /// Sets the properties of newinstance to the the properties of the oldinstance, if they are different
        /// </summary>
        /// <param name="oldinstance"></param>
        /// <param name="newinstance"></param>
        protected static void SetNestedClassProperties(object oldinstance, object newinstance)
        {
            // check if the nested properties are different?
            var oldprops = oldinstance.GetType().GetProperties();
            var newprops = newinstance.GetType().GetProperties();

            var props = oldprops.Join(newprops, oldpi => oldpi.Name, newpi => newpi.Name,
                                      (oldpi, newpi) => new { oldpi, newpi });
            foreach (var prop in props)
            {
                // see if any values are different
                var oldvalue = prop.oldpi.GetValue(oldinstance, null);

                var newvalue = prop.newpi.GetValue(newinstance, null);

                if (!Equals(oldvalue, newvalue))
                {
                    prop.oldpi.SetValue(oldinstance, newvalue, null);
                }
            }
        }
    }
}
