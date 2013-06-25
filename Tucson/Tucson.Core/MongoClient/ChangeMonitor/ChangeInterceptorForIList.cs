using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Castle.DynamicProxy;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Tucson.MongoClient.ChangeMonitor
{
    internal class ChangeInterceptorForIList : ChangeInterceptor
    {
        private bool _loading, _disposed;
        private readonly Lazy<IMongoChangeProxyCreator> _changeProxyCreator = new Lazy<IMongoChangeProxyCreator>(MongoChangeProxyFactory.CreateChangeProxyCreator);
		private readonly Dictionary<object, object> _proxyTargetDictionary = new Dictionary<object, object>();

        private enum ChangeEnums
        {
            FullSave,
            Push,
            PopLast,
            PopFirst,
            Change,
        }

        private enum NextForEachDo
        {
            Stop,
            Continue
        }

        private readonly List<ListChangeDetail> _changes = new List<ListChangeDetail>();
        private bool _hasFullSave;

        private class ListChangeDetail
        {
            public ChangeEnums Change { get; set; }
            public object Value { get; set; }
            public int? Index { get; set; }
        }

        /// <summary>
        /// Gets a value indicating if there are changes for this list
        /// </summary>
        public override bool HasChanges
        {
            get
            {
                if (_changes.Count > 0)
                    return true;

                var changes = false;

                ForEach((list, idx) =>
                            {
                                var monitor = _changeProxyCreator.Value.GetPropertyChangeMonitor(list[idx]);
                                if (monitor != null && monitor.HasChanges)
                                {
                                    changes = true;
                                    return NextForEachDo.Stop; // stop the loop
                                }
                                return NextForEachDo.Continue; // continue checking
                            });

                return changes;
            }
        }

        public override void ClearChanges()
        {
            base.ClearChanges();

            _changes.Clear();
            _hasFullSave = false;
            if (_changeProxyCreator.IsValueCreated)
            {
                ForEach((list, idx) =>
                {
                    var monitor = _changeProxyCreator.Value.GetPropertyChangeMonitor(list[idx]);
                    if (monitor != null)
                        monitor.ClearChanges();

                    return NextForEachDo.Continue; // continue clearing
                });
            }
        }

        /// <summary>
        /// Sets the UpdateBuilder with changes made
        /// </summary>
        /// <param name="parentName"></param>
        /// <param name="ub"></param>
        public override void SetChanges(string parentName, UpdateBuilder ub)
        {
            if (!_hasFullSave)
            {
                var pc = _changeProxyCreator.Value;
                ForEach((list, idx) =>
                            {
                                var monitor = pc.GetPropertyChangeMonitor(list[idx]);
                                if (monitor == null || !monitor.HasChanges)
                                    return NextForEachDo.Continue;

                                var indexfield = string.Format("{0}.{1}", parentName, idx);
                                monitor.SetChanges(indexfield, ub);
                                return NextForEachDo.Continue;
                            });
            }

        	List<BsonValue> pushes = null;

            foreach (var change in _changes)
            {
                switch (change.Change)
                {
                    case ChangeEnums.Push:
						if (pushes == null)
							pushes = new List<BsonValue>();

                        if (change.Value != null && IsWrappableObject(change.Value.GetType(), change.Value))
							pushes.Add(change.Value.ToBsonDocument());
                        else
							pushes.Add(BsonValue.Create(change.Value));
                        break;
                    case ChangeEnums.FullSave:
						ub.Set(parentName, BsonArray.Create(ToList()));
						break;
                    case ChangeEnums.PopLast:
                        ub.PopLast(parentName);
                        break;
                    case ChangeEnums.PopFirst:
                        ub.PopFirst(parentName);
                        break;
                    case ChangeEnums.Change:
                        if (!change.Index.HasValue)
                            throw new ApplicationException("Index missing for change operation");

                        var indexfield = string.Format("{0}.{1}", parentName, change.Index.Value);
						if (change.Value != null && IsWrappableObject(change.Value.GetType(), change.Value))
							ub.Set(indexfield, change.Value.ToBsonDocument());
						else if (change.Value != null)
							ub.Set(indexfield, BsonValue.Create(change.Value));
						else
							ub.Unset(indexfield);
                        break;
                    default:
                        throw new ApplicationException("Missing case for ChangeEnums value " + change.Change);
                }
            }

			if (pushes != null)
				ub.PushAll(parentName, pushes);
        }

        public override void SetProxyInstance(object instance, Type instanceType, object proxyInstance)
        {
            base.SetProxyInstance(instance, instanceType, proxyInstance);

            _loading = true;

            try
            {
                if (instanceType.IsGenericType && instance is IList)
                {
                    var genType = instanceType.GetGenericArguments()[0];

                    ForEach((list, idx) =>
                                {
                                    var value = list[idx];

                                    if (value == null)
                                        return NextForEachDo.Continue; // nothing to proxy

                                	var valueType = value.GetType();

                                    if (!IsProxyableType(valueType))
                                        return NextForEachDo.Continue; // not a proxy-able type

									// create the proxy
                                    ChangeInterceptor unused;
                                    var proxy = _changeProxyCreator.Value.AttachChangeMonitor(genType, value, out unused);

									// add it the dictionary for proxy->value
									_proxyTargetDictionary.Add(proxy, value);

                                    list[idx] = proxy;
                                    return NextForEachDo.Continue;
                                }
                        );
                }
            }
            finally
            {
                _loading = false;
            }
        }

        public override void Intercept(IInvocation invocation)
        {
            switch (invocation.Method.Name)
            {
                case "Add":
                    Enqueue(ChangeEnums.Push, invocation.Arguments[0]);
                    break;
                case "set_Item":
                    if (InterceptSetItem(invocation))
                        return;
                    break;
                case "Clear":
                case "Insert":
                case "Remove":
                    Enqueue(ChangeEnums.FullSave);
                    break;

                case "RemoveAt":
                    var idx = (int)invocation.Arguments[0];
                    if (idx == 0)
                        Enqueue(ChangeEnums.PopFirst);
                    else
                        Enqueue(ChangeEnums.FullSave);
                    break;
            }

            invocation.Proceed();
        }

        private bool InterceptSetItem(IInvocation invocation)
        {
            var index = (int) invocation.Arguments[0];
            var newvalue = invocation.Arguments[1];
            var oldvalue = ((IList) Instance)[index];

            if (Equals(newvalue, oldvalue))
                return true; // don't do anything

            if (newvalue == null || oldvalue == null || !IsWrappableObject(newvalue.GetType(), newvalue))
            {
                Enqueue(ChangeEnums.Change, newvalue, index);
                return false;
            }

            // check to see if any of the values are different
            SetNestedClassProperties(oldvalue, newvalue);
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;

				_changes.Clear();

                if (_changeProxyCreator.IsValueCreated)
                    _changeProxyCreator.Value.Dispose();
            }
        }

		private List<BsonValue> ToList()
		{
			var list = (IList)Instance;

			var genType = InstanceType.GetGenericArguments()[0];

			var result = list
				.Cast<object>()
				.Select(o =>
				        	{
								// see if the object is proxied?
				        		var a = o as IProxyTargetAccessor;
								if (a != null)
								{
									object proxyTarget;
									if (_proxyTargetDictionary.TryGetValue(o, out proxyTarget))
										o = proxyTarget;
								}
				        		BsonValue v;

								if (IsWrappableObject(genType, o))
									v = o.ToBsonDocument(genType);
								else
									v = BsonValue.Create(o);

				        		return v;
				        	})
				.ToList();

			return result;
		}

        private void ForEach(Func<IList, int, NextForEachDo> action)
        {
            var list = (IList)Instance;
            for (var idx = 0; idx < list.Count; idx++)
            {
                if (action(list, idx) == NextForEachDo.Stop)
                    break;
            }
        }

        private void Enqueue(ChangeEnums change, object value = null, int? index = null)
        {
            if (_loading)
                return;

            if (change == ChangeEnums.FullSave)
            {
                _changes.Clear();
                _changes.Add(new ListChangeDetail
                                 {
                                     Change = ChangeEnums.FullSave
                                 });
                _hasFullSave = true;
            }
            else if (!_hasFullSave)
                _changes.Add(new ListChangeDetail
                                 {
                                     Change = change,
                                     Value = value,
                                     Index = index
                                 });
        }

    }
}
