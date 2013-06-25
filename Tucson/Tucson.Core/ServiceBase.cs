using System;
using System.Configuration;
using System.Linq;

namespace Tucson
{
    /// <summary>
    /// Provides a simple base class for all Tucson services.  Currently only implements standard Dispose() pattern.
    /// </summary>
    public abstract class ServiceBase : DisposableBase
    {
    	private static readonly Lazy<AllowReadPreference> TheDefaultConfiguredReadPreferenceStatic;

		/// <summary>
		/// Gets the default configured read preference, computing the default only once.
		/// <seealso cref="GetDefaultAllowReadPreferenceFromConfig" />
		/// </summary>
		protected static AllowReadPreference DefaultConfiguredReadPreference
		{
			get { return TheDefaultConfiguredReadPreferenceStatic.Value; }
		}

		static ServiceBase()
		{
			TheDefaultConfiguredReadPreferenceStatic = new Lazy<AllowReadPreference>(GetDefaultAllowReadPreferenceFromConfig);
		}

		/// <summary>
		/// Gets the default configured read preference (appsettings key: DefaultAllowReadPreference).
		/// When the config value is DirtyOk, this method returns AllowReadPreference.DirtyOk otherwise, it returns AllowReadPreference.MustBeCommitted
		/// <seealso cref="DefaultConfiguredReadPreference" />
		/// </summary>
		protected static AllowReadPreference GetDefaultAllowReadPreferenceFromConfig()
		{
			var dv = ConfigurationManager.AppSettings["DefaultAllowReadPreference"];

			return string.Compare(dv, "DirtyOk",
								  StringComparison.OrdinalIgnoreCase) ==
				   0
					? AllowReadPreference.DirtyOk
					: AllowReadPreference.MustBeCommitted;
		}

		/// <summary>
		/// Performs an action within the context of a committed read by setting the ReadPreference flag to AllowReadPreference.MustBeCommitted.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="repository">A repository to set the AllowDirtyRead flag</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		protected void PerformCommittedReadAction(
			Action actionToPerform,
			params IReadOnlyRepository[] repository)
		{
			PerformReadPreferenceAction(
				AllowReadPreference.MustBeCommitted,
				actionToPerform,
				repository);
		}

		/// <summary>
		/// Performs an action within the context of a committed read by setting the ReadPreference flag to AllowReadPreference.MustBeCommitted.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="repository">A repository to set the AllowDirtyRead flag</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <returns>The result of the action</returns>
		protected T PerformCommittedReadAction<T>(
			Func<T> actionToPerform,
			params IReadOnlyRepository[] repository)
		{
			return
				PerformReadPreferenceAction(
					AllowReadPreference.MustBeCommitted,
					actionToPerform,
					repository);
		}

		/// <summary>
		/// Performs an action within the context of dirty reads.  Helps make sure repo's ReadPreference flag is set to AllowReadPreference.DirtyOk.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		protected void PerformDirtyReadAction(
			Action actionToPerform,
			params IReadOnlyRepository[] repository)
		{
			PerformReadPreferenceAction(
				AllowReadPreference.DirtyOk,
				actionToPerform,
				repository);
		}

		/// <summary>
		/// Performs an action within the context of dirty reads.  Helps make sure repo's ReadPreference flag is set to AllowReadPreference.DirtyOk.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		/// <returns>The result of the action</returns>
		protected T PerformDirtyReadAction<T>(
			Func<T> actionToPerform,
			params IReadOnlyRepository[] repository)
		{
			return
				PerformReadPreferenceAction(
					AllowReadPreference.DirtyOk,
					actionToPerform,
					repository);
		}

		/// <summary>
		/// Performs an action within the context of dirty or non-dirty reads.  Helps make sure repo's ReadPreference flag is set correctly.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="readPreference">The value of ReadPreference</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		protected void PerformReadPreferenceAction(
			AllowReadPreference readPreference,
			Action actionToPerform,
			params IReadOnlyRepository[] repository)
		{
			PerformReadPreferenceAction(
				readPreference,
				() =>
					{
						actionToPerform();
						return 0;
					},
				repository);
		}

		/// <summary>
		/// Performs an action within the context of dirty or non-dirty reads.  Helps make sure repo's ReadPreference flag is set correctly.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="readPreference">The value of ReadPreference</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		/// <returns>The result of the action</returns>
		protected T PerformReadPreferenceAction<T>(
			AllowReadPreference readPreference,
			Func<T> actionToPerform,
			params IReadOnlyRepository[] repository)
		{
			var repos = repository.ToList();
			var oldValues = repos.Select(r => r.ReadPreference).ToList();

			repos.ForEach(r => r.ReadPreference = readPreference);
			try
			{
				return actionToPerform();
			}
			finally
			{
				for (var i = 0; i < repos.Count; i++)
				{
					repos[i].ReadPreference = oldValues[i];
				}
			}
		}
    }
}