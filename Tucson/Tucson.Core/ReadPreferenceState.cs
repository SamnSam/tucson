using System;

namespace Tucson
{
	/// <summary>
	/// A class that controls the ReadPrefernce of an IReadOnlyRepository.  Place it in a using statement to automatically restore the read prefrence back to what it was.
	/// </summary>
	public class ReadPreferenceState : IDisposable
	{
		private readonly AllowReadPreference _oldPref;
		private readonly IReadOnlyRepository _repo;

		/// <summary>
		/// Creates a new ReadPreferenceState, setting the repository's ReadPreference to readPreference.  Dispose of this object to restore the repository's original ReadPreference
		/// </summary>
		/// <param name="repo">The repository</param>
		/// <param name="readPreference">The read preference</param>
		public ReadPreferenceState(IReadOnlyRepository repo, AllowReadPreference readPreference)
		{
			_oldPref = repo.ReadPreference;
			_repo = repo;

			repo.ReadPreference = readPreference;
		}

		/// <summary>
		/// Restores the repository read preference
		/// </summary>
		public void Dispose()
		{
			_repo.ReadPreference = _oldPref;
		}
	}
}
