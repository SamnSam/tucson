using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace Tucson.MongoClient
{
    /// <summary>
    /// A factory to store Mongo Maps.
    /// </summary>
    public static class MongoMapFactory
    {
        private static readonly Dictionary<string, IMongoMap> Maps = new Dictionary<string, IMongoMap>();

        /// <summary>
        /// Adds a MongoMap into the MapFactory.  The instance is added only once by the Type.
        /// </summary>
        /// <param name="instance">An instance of a MongoMap</param>
        /// <returns>True if the Mongo Map was added, False if it was already added</returns>
        public static bool AddMap(IMongoMap instance)
        {
            lock (Maps)
            {
                var t = instance.GetType().FullName;
                if (Maps.ContainsKey(t))
                    return false;

                //have been made obsolete: removed by sam flint on 4/18/2013
                //var conventions = new ConventionProfile();
                //conventions.SetIgnoreIfNullConvention(new AlwaysIgnoreIfNullConvention());
                //conventions.SetIgnoreExtraElementsConvention(new AlwaysIgnoreExtraElementsConvention());
                //BsonClassMap.RegisterConventions(conventions, instance.IsMappedType);

                var pack = new ConventionPack();
                pack.Add(new IgnoreIfNullConvention(true)); pack.Add(new IgnoreExtraElementsConvention(true));
                ConventionRegistry.Register("MongoMapFactory", pack, instance.IsMappedType); 


                instance.RegisterClassMaps();

                Maps.Add(t, instance);

                return true;
            }
        }

        /// <summary>
        /// Returns the mongodb (BSON) element name for a given property from a type
        /// </summary>
        /// <typeparam name="T">The type of the mongo class</typeparam>
        /// <typeparam name="TR">The type of the property</typeparam>
        /// <param name="me">An instance of type T</param>
        /// <param name="getProperty">An expression that returns a property from the type</param>
        /// <returns></returns>
        public static string GetMongoElementName<T, TR>(this T me, Expression<Func<T, TR>> getProperty)
        {
            return GetMongoElementName(typeof(T), getProperty.Body);
        }

        /// <summary>
        /// Returns the mongodb (BSON) element name for a given property from a type
        /// </summary>
        /// <typeparam name="T">The type of the mongo class</typeparam>
        /// <param name="getProperty">An expression that returns a property from the type</param>
        /// <returns></returns>
        public static string GetMongoElementName<T>(Expression<Func<T, object>> getProperty)
        {
            return GetMongoElementName(typeof(T), getProperty.Body);
        }

        /// <summary>
        /// Returns the mongodb (BSON) element name for a given field from a type
        /// </summary>
        /// <param name="t">The type of the mongo class</param>
        /// <param name="field">An expression that returns a property from the type</param>
        /// <returns></returns>
        public static string GetMongoElementName(Type t, string field)
        {
            var cm = BsonClassMap.LookupClassMap(t);
            if (cm == null)
                throw new ApplicationException("Unable to find BsonClassMap for type " + t.FullName);

            var mm = cm.GetMemberMap(field);
            if (mm == null)
                throw new ApplicationException("Unable to find member map for type " + t.FullName + " field " + field);

            return mm.ElementName;
        }

        private static string GetMongoElementName(Type t, Expression body)
        {
            var mem = body as MemberExpression;

            if (mem == null)
            {
                var uexp = body as UnaryExpression;
                if (uexp != null)
                    mem = uexp.Operand as MemberExpression;

                if (mem == null)
                    throw new ApplicationException("getProperty method must return a property!");
            }

            return GetMongoElementName(t, mem.Member.Name);
        }
    }
}
