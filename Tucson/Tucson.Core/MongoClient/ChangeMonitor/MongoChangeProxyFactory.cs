using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;

namespace Tucson.MongoClient.ChangeMonitor
{
	internal static class MongoChangeProxyFactory
    {
        private static readonly ProxyGenerator ProxyGen = new ProxyGenerator();

		private static readonly Dictionary<Type, Tuple<List<PropInfoGetSet>, Dictionary<string, PropInfoGetSet>>>
			ChangeMonitorNestedTypeDictionary
				= new Dictionary<Type, Tuple<List<PropInfoGetSet>, Dictionary<string, PropInfoGetSet>>>();

		public static IMongoChangeProxyCreator CreateChangeProxyCreator()
        {
            return new MongoChangeProxyCreator();
        }

		private class MongoChangeProxyCreator : IMongoChangeProxyCreator
		{
			private Dictionary<object, IMongoModelChangeMonitor> _proxyMap = new Dictionary<object, IMongoModelChangeMonitor>();

			public void Dispose()
			{
				var p = _proxyMap;
				_proxyMap = null;
				foreach (var v in p.Values)
					v.Dispose();
				p.Clear();
			}

			/// <summary>
			/// Returns the IPropertyChangeMonitor for an instance of a type
			/// </summary>
			/// <param name="proxy">The entity object</param>
			/// <returns>The property change monitor, or NULL if one is not found</returns>
			public IMongoModelChangeMonitor GetPropertyChangeMonitor<T>(T proxy)
			{
				lock (_proxyMap)
				{
					IMongoModelChangeMonitor interceptor;
					if (!ReferenceEquals(proxy, null) && _proxyMap.TryGetValue(proxy, out interceptor))
						return interceptor;

					return null;
				}
			}

			public T AttachChangeMonitor<T>(T entity) where T : class
			{
				if (entity == null)
					return null;

				ChangeInterceptor unused;
				return (T) AttachChangeMonitor(entity.GetType(), entity, out unused);
			}

			public object AttachChangeMonitor(Type entityType, object entity, out ChangeInterceptor interceptor)
			{
				if (entityType == null)
					throw new ArgumentNullException("entityType");

				if (entity == null)
					throw new ArgumentNullException("entity");

				lock (_proxyMap)
				{
					interceptor = (PropertyChangeInterceptor) GetPropertyChangeMonitor(entity);
					if (interceptor != null)
						return interceptor.ProxyInstance;


					var islist = entityType.IsGenericType && entity is IList;
					object proxy;

					if (islist)
					{
						interceptor = new ChangeInterceptorForIList();
						proxy = ProxyGen.CreateInterfaceProxyWithTarget(entityType,
						                                                new[] {typeof (IList)},
						                                                entity,
						                                                interceptor);
					}

					else
					{
						CreateMonitorsForClassProps(entityType, entity, out interceptor, out proxy);
					}

					interceptor.SetProxyInstance(entity, entityType, proxy);

					_proxyMap.Add(proxy, interceptor);

					return proxy;
				}
			}

			private void CreateMonitorsForClassProps(
				Type entityType,
				object entity,
				out ChangeInterceptor interceptor,
				out object proxy)
			{
				List<IMongoModelChilldPropertyChangeMonitor> childInterceptors = null;
				IEnumerable<Tuple<PropInfoGetSet, object>> nestedArrays = null;

				var propListAndDict = GetPropInfoGetSet(entityType);
				var props = propListAndDict.Item1;

				// now add change monitors for each of the classes nested properties
				var nestedpropswithvalues =
					props
						.Select(p => new Tuple<PropInfoGetSet, object>(p, p.Getter(entity)))
						.Where(pv => pv.Item2 != null)
						.ToList();

				if (nestedpropswithvalues.Count > 0)
				{
					childInterceptors = new List<IMongoModelChilldPropertyChangeMonitor>();

					nestedArrays = nestedpropswithvalues.Where(n => n.Item1.Property.PropertyType.IsArray);

					// re-assign each of the nested properties to a proxied object
					foreach (var nestedprop in nestedpropswithvalues.Where(n => !n.Item1.Property.PropertyType.IsArray))
					{
						ChangeInterceptor nestedint;
						var newv = AttachChangeMonitor(nestedprop.Item1.Property.PropertyType, nestedprop.Item2, out nestedint);

						if (newv == null)
							continue;
						
						nestedprop.Item1.Setter(entity, newv);

						childInterceptors.Add(
							new ChildPropertyChangeInterceptor
								{
									PropInfo = nestedprop.Item1,
									Monitor = nestedint
								});
					}
				}

				var pci = new PropertyChangeInterceptor(propListAndDict.Item2);

				// add any nested array properties
				if (nestedArrays != null)
					nestedArrays.ForAll(t => pci.AddArrayPropertyMonitor(t.Item1, (Array) t.Item2));

				// generate the proxy
				proxy = ProxyGen.CreateClassProxyWithTarget(entityType, entity, pci);
				pci.ChildChangeMonitors = childInterceptors;

				interceptor = pci;
			}

			private static Dictionary<string, PropInfoGetSet> GetPropInfoGetSetDictionary(Type entityType)
			{
				return GetPropInfoGetSet(entityType).Item2;
			}

			private static List<PropInfoGetSet> GetPropInfoGetSetList(Type entityType)
			{
				return GetPropInfoGetSet(entityType).Item1;
			}

			private static Tuple<List<PropInfoGetSet>, Dictionary<string, PropInfoGetSet>> GetPropInfoGetSet(Type entityType)
			{
				Tuple<List<PropInfoGetSet>, Dictionary<string, PropInfoGetSet>> props;
				lock (ChangeMonitorNestedTypeDictionary)
				{
					if (!ChangeMonitorNestedTypeDictionary.TryGetValue(entityType, out props))
					{
						var strtype = typeof (string);

						var list = entityType
							.GetMembers()
							.Where(m => m.MemberType == MemberTypes.Property)
							.Select(m => (PropertyInfo) m)
							.Select(p => new
							             	{
							             		proxyable = (p.PropertyType.IsClass || p.PropertyType.IsInterface) &&
							             		            !strtype.IsAssignableFrom(p.PropertyType),
							             		pi = new PropInfoGetSet(p)
							             	})
							.ToList();

						var dict = new Dictionary<string, PropInfoGetSet>();
						foreach (var item in list.Select(at => at.pi))
						{
							dict[item.Property.Name] = item;
							dict["set_" + item.Property.Name] = item;
							dict["get_" + item.Property.Name] = item;
						}

						props = new Tuple<List<PropInfoGetSet>, Dictionary<string, PropInfoGetSet>>(
							list.Where(at => at.proxyable).Select(at => at.pi).ToList(),
							dict);

						ChangeMonitorNestedTypeDictionary.Add(entityType, props);
					}
				}
				return props;
			}
		}

		private class ChildPropertyChangeInterceptor : IMongoModelChilldPropertyChangeMonitor
        {
            public PropInfoGetSet PropInfo { get; set; }

            public IMongoModelChangeMonitor Monitor { get; set; }
        }

    }
}
