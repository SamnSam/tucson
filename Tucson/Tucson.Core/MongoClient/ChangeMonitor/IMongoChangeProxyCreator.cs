using System;

namespace Tucson.MongoClient.ChangeMonitor
{
	internal interface IMongoChangeProxyCreator : IDisposable
	{
		IMongoModelChangeMonitor GetPropertyChangeMonitor<T>(T proxy);
		T AttachChangeMonitor<T>(T entity) where T : class;
		object AttachChangeMonitor(Type entityType, object entity, out ChangeInterceptor interceptor);
	}
}
