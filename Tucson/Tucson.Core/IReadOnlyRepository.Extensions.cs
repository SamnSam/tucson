using System;

namespace Tucson
{
	/// <summary>
	/// Some extensions for the IReadOnlyRepository
	/// </summary>
	public static class IReadOnlyRepositoryExtensions
	{
		/// <summary>
		/// Performs an action within the context of a committed read by setting the ReadPreference flag to AllowReadPreference.MustBeCommitted.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="repository">A repository to set the AllowDirtyRead flag</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		public static void PerformCommittedReadAction(
			this IReadOnlyRepository repository,
			Action actionToPerform)
		{
			PerformReadPreferenceAction(
				repository,
				AllowReadPreference.MustBeCommitted,
				actionToPerform
				);
		}

		/// <summary>
		/// Performs an action within the context of a committed read by setting the ReadPreference flag to AllowReadPreference.MustBeCommitted.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="repository">A repository to set the AllowDirtyRead flag</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <returns>The result of the action</returns>
		public static T PerformCommittedReadAction<T>(
			this IReadOnlyRepository repository,
			Func<T> actionToPerform)
		{
			return
				PerformReadPreferenceAction(
					repository,
					AllowReadPreference.MustBeCommitted,
					actionToPerform
					);
		}

		/// <summary>
		/// Performs an action within the context of dirty reads.  Helps make sure repo's ReadPreference flag is set to AllowReadPreference.DirtyOk.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		public static void PerformDirtyReadAction(
			this IReadOnlyRepository repository,
			Action actionToPerform)
		{
			PerformReadPreferenceAction(
				repository,
				AllowReadPreference.DirtyOk,
				actionToPerform
				);
		}

		/// <summary>
		/// Performs an action within the context of dirty reads.  Helps make sure repo's ReadPreference flag is set to AllowReadPreference.DirtyOk.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		/// <returns>The result of the action</returns>
		public static T PerformDirtyReadAction<T>(
			this IReadOnlyRepository repository,
			Func<T> actionToPerform)
		{
			return
				PerformReadPreferenceAction(
					repository,
					AllowReadPreference.DirtyOk,
					actionToPerform
					);
		}

		/// <summary>
		/// Performs an action within the context of dirty or non-dirty reads.  Helps make sure repo's ReadPreference flag is set correctly.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		/// <param name="readPreference">The value of ReadPreference</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		public static void PerformReadPreferenceAction(
			this IReadOnlyRepository repository,
			AllowReadPreference readPreference,
			Action actionToPerform)
		{
			PerformReadPreferenceAction(
				repository,
				readPreference,
				() =>
					{
						actionToPerform();
						return 0;
					});
		}

		/// <summary>
		/// Performs an action within the context of dirty or non-dirty reads.  Helps make sure repo's ReadPreference flag is set correctly.
		/// ReadPreference is restored after the completion of the action.
		/// </summary>
		/// <param name="readPreference">The value of ReadPreference</param>
		/// <param name="actionToPerform">The action delegate to perform</param>
		/// <param name="repository">A repository to set the ReadPreference flag</param>
		/// <returns>The result of the action</returns>
		public static T PerformReadPreferenceAction<T>(
			this IReadOnlyRepository repository,
			AllowReadPreference readPreference,
			Func<T> actionToPerform)
		{
			using (new ReadPreferenceState(repository, readPreference))
				return actionToPerform();
		}
	}
}
