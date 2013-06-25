using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using MongoDB.Driver;

namespace Tucson.MongoClient
{
    internal static class MongoUrlFactory
    {
        private static readonly Dictionary<string, MongoUrl>[] CachedBuilders;
        private static readonly object SyncLock = new object();
 
        // ReSharper disable InconsistentNaming
        public const string PROTOCOL = "mongodb://";
        private const string PASSWORD = "<cryptokey>";///this is used with some cryptography dll
        // ReSharper restore InconsistentNaming

		static MongoUrlFactory()
		{
			CachedBuilders =
				Enumerable
					.Range(0, Enum.GetNames(typeof (ReadPreferenceMode)).Length + 1)
					.Select(i => new Dictionary<string, MongoUrl>())
					.ToArray();

			// the code below just makes sure that ReadPreferenceMode is 0..n for each item in the enumeration
			var x =
				Enum.GetValues(typeof (ReadPreferenceMode))
					.OfType<int>()
					.ToList();

			for (var i =0 ;i<x.Count;i++)
			{
				if (i != x[i])
					throw new ApplicationException("Uh, oh!  Mongo's ReadPreferenceMode is not an expected value!");
			}
		}

		private static IDictionary<string, MongoUrl> GetCachedBuilder(ReadPreferenceMode? readMode)
		{
			return readMode == null
			       	? CachedBuilders[CachedBuilders.Length - 1]
			       	: CachedBuilders[(int) readMode.Value];
		}

    	/// <summary>
        /// Gets a MongoUrl for the connection string.
        /// </summary>
        /// <param name="connectionString">Connection string or named connection string.</param>
		/// <param name="readMode">The read preference</param>
        /// <param name="socketTimeout">The timeout for socket calls</param>
        /// <returns>MongoUrl derived from System.Uri.</returns>
        public static MongoUrl GetUrl(string connectionString, ReadPreferenceMode? readMode, TimeSpan? socketTimeout = null)
        {
            MongoUrl url;

    		var cachedBuilder = GetCachedBuilder(readMode);

			if (!cachedBuilder.TryGetValue(connectionString, out url))
            {
                lock (SyncLock)
                {
					if (!cachedBuilder.TryGetValue(connectionString, out url))
                    {
                        var x = new MongoUrlBuilder(GetConnectionString(connectionString));
                        url = x.ToMongoUrl();

                        try
                        {
                            // see if we can decrypt the password
                            url = DecryptPassword(url);
                        }
                        // ReSharper disable EmptyGeneralCatchClause
                        catch
                        // ReSharper restore EmptyGeneralCatchClause
                        {

                        }

						cachedBuilder.Add(connectionString, url);
                    }
                }
            }

            if (socketTimeout != null)
            {
                var ub = new MongoUrlBuilder(url.Url)
                             {
                                 SocketTimeout = socketTimeout.Value
                             };
                url = ub.ToMongoUrl();
            }

            return url;
        }

        /// <summary>
        /// Updates the cached url for a given connection string
        /// </summary>
        /// <param name="connectionString">The connection string value</param>
		/// <param name="readMode">The read preference</param>
        /// <param name="url">The new Url</param>
		public static void UpdateUrl(string connectionString, ReadPreferenceMode? readMode, MongoUrl url)
        {
            lock (SyncLock)
            {
				var cachedBuilder = GetCachedBuilder(readMode);

				cachedBuilder.Remove(connectionString);
                // make sure the cached url doesn't have a custom timeout
                var ubstd = new MongoUrlBuilder();
                var ub = new MongoUrlBuilder(url.Url)
                             {
                                 SocketTimeout = ubstd.SocketTimeout
                             };
                url = ub.ToMongoUrl();

				cachedBuilder.Add(connectionString, url);
            }
        }

        /// <summary>
        /// Removes connection string from cache
        /// </summary>
        /// <param name="connectionString">Connection string to remove</param>
        /// <param name="readMode">The read preference</param>
        public static void Clear(string connectionString, ReadPreferenceMode? readMode)
        {
            lock (SyncLock)
            {
				var cachedBuilder = GetCachedBuilder(readMode);
				cachedBuilder.Remove(connectionString);
            }
        }

        /// <summary>
        /// Gets a connection string using either a named string or a properly formatted string.
        /// </summary>
        /// <param name="connectionString">A named string in App.config or a connection string</param>
        /// <returns>A properly formatted connection string.</returns>
        private static string GetConnectionString(string connectionString)
        {
            var connection = connectionString;

            //Check if the string is a connection string.
            if (!connection.StartsWith(PROTOCOL, StringComparison.InvariantCultureIgnoreCase))
            {
                try
                {
                    //Try to retrieve the named string from the configuration file.
                    connection = ConfigurationManager.ConnectionStrings[connectionString].ConnectionString;
                }
                catch (NullReferenceException)
                {
                    //Invalid format.
                    throw new ArgumentException("Connection String must start with 'mongodb://' or be the name of a connection string in the app.config.");
                }
            }

            return connection;
        }

        /// <summary>
        /// Creates new MongoUrl instance based on username and decrypted password
        /// </summary>
        /// <param name="url"></param>
        /// <returns>MongoUrl with username and decrypted password</returns>
        private static MongoUrl DecryptPassword(MongoUrl url)
        {
            if (url == null || url.Username == null || url.Password == null)
                return url;


			///use whatever crypto dll you have
			//crypto is not open source but you can use whatever you have
			/*
            var crypto = //crypto declaration 

            var bytes = Encoding.UTF8.GetBytes(url.Username).ToList();
            while (bytes.Count < 8)
                bytes.AddRange(Encoding.UTF8.GetBytes("-"));
            crypto.Salt = bytes.ToArray();

            var pwd = crypto.Decrypt(url.Password, PASSWORD);

            */
			return new MongoUrlBuilder(url.Url)
            {
                Username = url.Username,
                Password = url.Password
            }.ToMongoUrl();
        }
    }
}
