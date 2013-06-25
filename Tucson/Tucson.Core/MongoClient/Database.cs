using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MongoDB.Driver;

namespace Tucson.MongoClient
{
	/// <summary>
	/// Represents a MongoDB database and the settings used to access it. 
	/// This class is thread-safe.
	/// </summary>
	public class Database : DisposableBase, ICollectionProvider
	{
		private readonly string _connectionString;
		private MongoDatabase _database;
		private int? _serverInstances;
		private TimeSpan? _timeout;

		private enum ConnectionState
		{
			/// <summary>
			/// Initial state
			/// </summary>
			FirstConnect,
			/// <summary>
			/// Connection failed, try no authentication
			/// </summary>
			FailBackToNoAuth,
			/// <summary>
			/// Connection failed, try authentication
			/// </summary>
			FailBackToAuth,
		}

		/// <summary>
		/// Instantiates a new Repository and database context for the entity.
		/// </summary>
		/// <param name="connectionString">MongoDB connection string.</param>
		public Database(string connectionString)
		{
			_connectionString = connectionString;
		}

		/// <summary>
		/// Gets or sets the timeout for the collection
		/// </summary>
		public TimeSpan? Timeout
		{
			get { return _timeout; }

			set
			{
				if ((_timeout == null && value != null) || (_timeout != null && value == null))
				{
					_timeout = value;
					_database = null;
					_serverInstances = null;
				}
				else if (_timeout != null && value != null)
				{
					// ReSharper disable CompareOfFloatsByEqualityOperator
					if (_timeout.Value.TotalMilliseconds != value.Value.TotalMilliseconds)
					{
						_timeout = value;
						_database = null;
						_serverInstances = null;
					}
					// ReSharper restore CompareOfFloatsByEqualityOperator
				}
			}
		}

		/// <summary>
		/// Gets a value indicating the number of server instances for the connection
		/// </summary>
		public int ServerInstances
		{
            get 
            { 
                return _serverInstances ?? (_serverInstances = ActOnDatabase(d =>
                    {
                        // This statement forces a database connection to be established
                        d.CollectionExists("");

                        return d.Server.Instances.Length;
                    })).Value; 
            }
		}

		protected T ActOnDatabase<T>(Func<MongoDatabase, T> act)
		{
			return ActOnDatabase(act, null);
		}

		protected T ActOnDatabase<T>(Func<MongoDatabase, T> act, ReadPreferenceMode? readMode)
		{
			// endless retry loop: we actually try the login at most 2 times
            // 1st time: with a decrypted password or whatever the connection string says (if decryption fails)
            // 2nd time: with no credentials
			MongoUrl url = null;
			var state = ConnectionState.FirstConnect;

		    MongoCredential credentialsUsed = null;

            var endOfStreamCounter = 0;
			while (true)
			{
				try
				{
					if (_database == null ||
						(readMode.HasValue && _database.Server.Settings.ReadPreference.ReadPreferenceMode != readMode.Value))
					{
						// first call
						if (url == null)
							url = MongoUrlFactory.GetUrl(_connectionString, readMode, Timeout);

						if (readMode.HasValue)
						{
							// strip out the "slaveOk=true" or "slaveOk=false" or any readPreference setting, since otherwise we 
                            // get an exception setting ReadPreference
						    var rawUrl = RemoveReadPreference(url.ToString());

							var ub = new MongoUrlBuilder(rawUrl);

							ub.ReadPreference = new ReadPreference(readMode.Value);

							url = ub.ToMongoUrl();
						}

						_database = MongoDatabase.Create(url);
					}

					if (url != null && url.Username != null)
					{
					    credentialsUsed = new MongoCredential(
					        "whatever", new MongoInternalIdentity(url.DatabaseName, url.Username), new PasswordEvidence(url.Password));
					}

				    // do something to the mongo database
					var result = act(_database);

					if (state != ConnectionState.FirstConnect)
					{
						// update the cached url with the url we were able to connect with, this avoids overhead on subsequent calls
						MongoUrlFactory.UpdateUrl(_connectionString, readMode, url);
					}

					return result;
				}
				catch (MongoException me)
				{
					if (url == null)
						throw; // something really bad happened

					switch (state)
					{
						case ConnectionState.FirstConnect:
					        state = ConnectionState.FailBackToNoAuth;
							break;
						case ConnectionState.FailBackToAuth:
							// re-throw exception with detail about what db we're connecting to
							throw new MongoException(
								string.Format("Unable to connect to mongo server(s): {0} database:{1} user:{2} safeMode:{3}",
								              string.Join(",", url.Servers.Select(s => s.ToString())),
								              url.DatabaseName,
								              credentialsUsed == null
								              	? "(null)"
								              	: credentialsUsed.Username,
								              url.SafeMode),
								me);
                        case ConnectionState.FailBackToNoAuth:
							state = ConnectionState.FailBackToAuth;
							break;
					}
				}
				catch (EndOfStreamException)
				{
                    // Start over
                    if (state == ConnectionState.FirstConnect && endOfStreamCounter < 3)
                    {
                        _database = null;
                        url = null;
                        credentialsUsed = null;
                        Thread.Sleep(endOfStreamCounter * 100);
                        endOfStreamCounter++;
                        continue;
                    }

				    throw;
				}

				switch (state)
				{
					case ConnectionState.FirstConnect:
						throw new ApplicationException("Invalid state");
					case ConnectionState.FailBackToAuth:
				        CollectionCreatedCheck.Clear();
						MongoUrlFactory.Clear(_connectionString, readMode);
						url = MongoUrlFactory.GetUrl(_connectionString, readMode, Timeout);
						break;
					case ConnectionState.FailBackToNoAuth:
						// remove the credentials from the URL altogether
						url = RemovePassword(url);
						break;
				}

				// release the database and try again
				_database = null;
				_serverInstances = null;
			}
		}

		/// <summary>
		/// Gets a MongoCollection instance representing a collection on this 
		/// database with a default document type of T.
		/// </summary>
		/// <typeparam name="T">The default document type for this collection.</typeparam>
		/// <param name="collectionName">The name of the collection.</param>
		/// <returns>An instance of MongoCollection.</returns>
		public MongoCollection<T> GetCollection<T>(string collectionName) where T : class
		{
			return GetCollection<T>(collectionName, null);
		}

		/// <summary>
		/// Gets a MongoCollection instance representing a collection on this 
		/// database with a default document type of T.
		/// </summary>
		/// <typeparam name="T">The default document type for this collection.</typeparam>
		/// <param name="collectionName">The name of the collection.</param>
		/// <param name="options">Options for creating this collection.</param>
		/// <returns>An instance of MongoCollection.</returns>
		public MongoCollection<T> GetCollection<T>(string collectionName, IMongoCollectionOptions options)
			where T : class
		{
			return GetCollection<T>(collectionName, options, null);
		}

		/// <summary>
		/// Returns a MongoCollection instance for access to documents of the 
		/// given type.
		/// </summary>
		/// <typeparam name="T">Type of the entity.</typeparam>
		/// <param name="collectionName">The name of the collection.</param>
		/// <param name="options">Options for creating this collection.</param>
		/// <param name="readMode">The read mode preference</param>
		/// <returns>MongoCollection of type <typeparamref name="T"/>.</returns>
		public MongoCollection<T> GetCollection<T>(string collectionName, IMongoCollectionOptions options, ReadPreferenceMode? readMode)
			where T : class
		{
			return ActOnDatabase(d => GetCollection<T>(d, collectionName, options), readMode);
		}

		/// <summary>
		/// Creates new MongoUrl instance with no credentials
		/// </summary>
		/// <param name="url"></param>
		/// <returns>MongoUrl with no credentials</returns>
		private static MongoUrl RemovePassword(MongoUrl url)
		{
			return new MongoUrlBuilder(url.Url)
			       	{
                        Username = null,
                        Password = null
			       	}
				.ToMongoUrl();
		}

        private static readonly Regex ConnectionStringPattern = new Regex(
            "(?<start>(;|&|\\?))" +                             // Match an option separator or the "?" character (start of option list)
            "(?<remove>(slaveok=\\w+|readpreference=\\w+))" +   // Match "slaveOk" or "readPreference" option
            "(?<end>(;|&|$))",                                  // Match an option separator or the end of the string
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// If a ReadPreferenceMode is specified when connecting to Mongo, the "slaveOk" or "readPreference" option 
        /// needs to be removed from the connection string we are using. This is because the Mongo driver will complain
        /// if the readPreference is set manually after it's already been specified in the connection string. 
        /// </summary>
        /// <param name="rawConnectionString">The unmodified connection string</param>
        /// <returns>The connection string with the "slaveOk" or "readPreference" option removed</returns>
        internal static string RemoveReadPreference(string rawConnectionString)
        {
            var match = ConnectionStringPattern.Match(rawConnectionString);

            if (!match.Success)
                return rawConnectionString;

            var isFirst = string.Equals(match.Groups["start"].Value, "?");
            var isLast = string.IsNullOrEmpty(match.Groups["end"].Value);

            var replacement =
                // If it's the last option, or in the middle of a group of options, remove the preceding character.
                (isLast || !isFirst ? match.Groups["start"].Value : string.Empty) + 
                // Removed the matched option and associated value (slaveOk or readPreference).
                match.Groups["remove"].Value +
                // If it's the first option, remove the subsequent character.
                (isFirst ? match.Groups["end"].Value : string.Empty);

            var result = rawConnectionString.Replace(replacement, string.Empty);

            return result;
        }

		/// <summary>
		/// Gets a MongoCollection using the MongoDB driver.
		/// </summary>
		/// <param name="database">The database for MongoDB.</param>
		/// <param name="collectionName">Name of the collection to retrieve.</param>
		/// <param name="options">Settings for the collection.</param>
		/// <returns>MongoCollection</returns>
		private static MongoCollection<T> GetCollection<T>(MongoDatabase database, string collectionName,
														   IMongoCollectionOptions options) where T : class
		{
		    var key = collectionName + database.Settings.ToString();

			var t = CollectionCreatedCheck.GetOrAdd(
				key,
				cn =>
				{
					if (!database.GetCollectionNames().Contains(collectionName))
                        database.CreateCollection(collectionName, options); // creates the database
					return true;
				});

			if (!t)
			    throw new ApplicationException("Unexpected behavior: CollectionCreatedCheck returned false");

			return database.GetCollection<T>(collectionName);
		}

		/// <summary>
		/// Disposes of any objects created by the database
		/// </summary>
		/// <param name="disposing">True when user is calling Dispose(), false if coming from GC</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_database = null;
		}

		private static readonly ConcurrentDictionary<string, bool> CollectionCreatedCheck = new ConcurrentDictionary<string, bool>();
	}
}