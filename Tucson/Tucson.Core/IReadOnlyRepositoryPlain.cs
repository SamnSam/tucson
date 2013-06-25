using System;

namespace Tucson
{
	/// <summary>
	/// Generic interface for implementing the Repository pattern over
	/// business entities.
	/// </summary>
	public interface IReadOnlyRepository : IDisposable
	{
		/// <summary>
		/// Gets or sets a timeout for the repository
		/// </summary>
		TimeSpan? Timeout { get; set; }

		/// <summary>
		/// Gets or sets a flag to default to reading all data using potentially uncommitted/dirty reads
		/// </summary>
		AllowReadPreference ReadPreference { get; set; }
	}
}
