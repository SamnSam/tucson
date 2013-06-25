namespace Tucson
{
	/// <summary>
	/// Flags to specify how reads should be performed within a repository
	/// </summary>
	public enum AllowReadPreference
	{
		/// <summary>
		/// Data reads come from whatever the connection string says (eg: SlaveOk=true in mongo)
		/// </summary>
		Default,

		/// <summary>
		/// Data reads must come from committed data
		/// </summary>
		MustBeCommitted,

		/// <summary>
		/// Data reads can be dirty/uncommitted/replicated data
		/// </summary>
		DirtyOk
	}
}
