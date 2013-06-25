using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Castle.DynamicProxy;
using MongoDB.Bson;

namespace Tucson.MongoClient.ChangeMonitor
{
    internal class PropertyChangeInterceptor : ChangeInterceptor, IMongoModelPropertyChangeMonitor
    {
		private readonly Dictionary<string, PropInfoGetSet> _props;
		private Lazy<Dictionary<PropInfoGetSet, bool>> _dirtyPropNames;
		private List<OriginalArrayValue> _originalArrayValues;

		private class OriginalArrayValue
		{
			public OriginalArrayValue(PropInfoGetSet prop, Array value)
			{
				PropInfo = prop;
				Value = value;
			}

			public PropInfoGetSet PropInfo { get; private set; }
			public Array Value { get; set; }
			public bool Skip { get; set; }
		}

		public PropertyChangeInterceptor(Dictionary<string, PropInfoGetSet> props)
		{
			_dirtyPropNames = new Lazy<Dictionary<PropInfoGetSet, bool>>();
			_props = props;
		}

        public IEnumerable<string> DirtyProperties
        {
            get { return _dirtyPropNames.Value.Keys.Select(pi => pi.Property.Name); }
        }

        public override bool HasChanges
        {
            get
            {
                if (_dirtyPropNames.IsValueCreated && _dirtyPropNames.Value.Count > 0)
                    return true; // have dirty properties

                if (ChildChangeMonitors == null)
                    return false; // no child monitors

                // return if any child monitors have changes
                return ChildChangeMonitors.Any(ccm => ccm.Monitor.HasChanges);
            }

        }

        public override void ClearChanges()
        {
            base.ClearChanges();
            
            // reset the dirty property names
            if (_dirtyPropNames.IsValueCreated)
				_dirtyPropNames = new Lazy<Dictionary<PropInfoGetSet, bool>>();
        }

        /// <summary>
        /// Sets the UpdateBuilder with changes made
        /// </summary>
        /// <param name="parentName"></param>
        /// <param name="ub"></param>
        public override void SetChanges(string parentName, MongoDB.Driver.Builders.UpdateBuilder ub)
        {
        	List<PropInfoGetSet> skip = null;

            if (_dirtyPropNames.IsValueCreated)
            {
                // generate an update statement for all the updated fields
                foreach (var field in _dirtyPropNames.Value.Keys)
                {
                    var fn = GetMongoName(parentName, field.Property.Name);
                    var v = field.Getter(ProxyInstance);

					if (_originalArrayValues != null && field.Property.PropertyType.IsArray)
					{
						var avIdx = _originalArrayValues.FindIndex(t => t.PropInfo == field);
						if (avIdx >= 0)
						{
							if (v == null)
								_originalArrayValues.RemoveAt(avIdx);
							else
							{
								var av = _originalArrayValues[avIdx];
								av.Value = (Array) v;
								av.Skip = true;
							}
						}
					}

                    if (v == null)
                    {
                        ub.Unset(fn);
                    }
                    else if (IsWrappableObject(v.GetType(), v))
                    {
                        // this is a class
                        ub.SetWrapped(fn, v);
                    }
					else if (v is ICollection && !field.Property.PropertyType.IsArray)
					{
						var a = new BsonArray();

						ListUpdate((ICollection)v, a);

						ub.Set(fn, a);
					}
					else
						ub.Set(fn, BsonValue.Create(v));
                }
            }

			if (_originalArrayValues != null)
			{
				foreach (var item in _originalArrayValues)
				{
					if (item.Skip)
					{
						item.Skip = false;
						continue;
					}

					var fn = GetMongoName(parentName, item.PropInfo.Property.Name);
					var v = item.PropInfo.Getter(ProxyInstance) as Array;

					if (v == null)
					{
						// it had a value, now it doesn't
						ub.Unset(fn);
					}
					else if (!ArraysAreTheSame(v, item.Value))
					{
						// use BsonValue - this may throw exceptions for arrays containing non-primitive types, dunno.  Only really tested it on byte[]
						ub.Set(fn, BsonValue.Create(v));
					}
				}
			}

            if (ChildChangeMonitors != null)
            {
                // check all the child monitors
                foreach (var child in ChildChangeMonitors)
                {
                    var v = child.PropInfo.Getter(ProxyInstance);
                    var pv = child.Monitor.ProxyInstance;
                    if (!ReferenceEquals(v, pv))
                        continue;

                    var childname = MongoMapFactory.GetMongoElementName(InstanceType, child.PropInfo.Property.Name);
                    var propname = parentName == null
                                       ? childname
                                       : string.Format("{0}.{1}",
                                                       parentName,
                                                       childname);

                    child.Monitor.SetChanges(propname, ub);
                }
            }
        }

		private bool ArraysAreTheSame(Array left, Array right)
		{
			if (left == null || right == null)
				return false;
			if (left.Length != right.Length)
				return false;

			for (var i = 0; i < left.Length; i++)
			{
				if (left.GetValue(i) != right.GetValue(i))
					return false;
			}

			return false;
		}

        /// <summary>
        /// handles update statements for lists
        /// </summary>
        /// <param name="list"></param>
        /// <param name="bsonArray"></param>
        private void ListUpdate(ICollection list, BsonArray bsonArray)
        {
            foreach (var o in list)
            {
                if (o != null && IsWrappableObject(o.GetType(), o))
                    bsonArray.Add(o.ToBsonDocument());
                else if (o is ICollection)
                {
                    var a = new BsonArray();

                    ListUpdate((ICollection) o, a);

                    bsonArray.Add(a);
                }
                else
                    bsonArray.Add(BsonValue.Create(o));
            }
        }

		internal void AddArrayPropertyMonitor(PropInfoGetSet property, Array originalValue)
		{
			if (_originalArrayValues == null)
				_originalArrayValues = new List<OriginalArrayValue>();

			_originalArrayValues.Add(new OriginalArrayValue(property, (Array)originalValue.Clone()));
		}

        /// <summary>
        /// called when any virtual member of the model is used
        /// </summary>
        /// <param name="invocation"></param>
        public override void Intercept(IInvocation invocation)
        {
        	var method = invocation.Method.Name;
			if (method.StartsWith("set_") && invocation.Arguments.Length == 1)
            {
                // all property set calls begin with "set_"
                // var propName = invocation.Method.Name.Substring(4);

            	PropInfoGetSet propInfo;

				if (_props.TryGetValue(method, out propInfo))
                {
					var oldvalue = propInfo.Getter(invocation.InvocationTarget);
                    var newvalue = invocation.Arguments[0];

                    if (IsValueEqual(oldvalue, newvalue) == ValueResultEnum.NotChanged)
                        return;

                    AddDirtyProp(propInfo);
                }
				else
					throw new ApplicationException("Unexpected set_ call to a non-property!");
            }

            invocation.Proceed();
        }

        protected override void Dispose(bool disposing)
        {
            // noop
        }

        protected void AddDirtyProp(PropInfoGetSet propInfo)
        {
            lock (_dirtyPropNames)
            {
                bool cnt;
				if (!_dirtyPropNames.Value.TryGetValue(propInfo, out cnt))
					_dirtyPropNames.Value.Add(propInfo, true);
            }
        }
    }

}
