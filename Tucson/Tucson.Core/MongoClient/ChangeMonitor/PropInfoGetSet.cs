using System;
using System.Reflection;

namespace Tucson.MongoClient.ChangeMonitor
{
	internal class PropInfoGetSet
	{
		private readonly Lazy<Func<object, object>> _getter;
		private readonly Lazy<Action<object, object>> _setter;

		public PropInfoGetSet(PropertyInfo prop)
		{
			Property = prop;
			//this uses proprietary code
			//but you could define DynamicReflectionDelegateFactory.CreateGetPropertyValue and create your own reflection factory
			//_getter = new Lazy<Func<object, object>>(() => DynamicReflectionDelegateFactory.CreateGetPropertyValue<object>(prop));
			//_setter = new Lazy<Action<object, object>>(() => DynamicReflectionDelegateFactory.CreateSetPropertyValue<object>(prop));
		}

		public PropertyInfo Property { get; private set; }
		public Func<object, object> Getter { get { return _getter.Value; } }
		public Action<object, object> Setter { get { return _setter.Value; } }
	}
}
