using System;

namespace Tucson.Provider
{
	/// <summary>
	/// Provider factory for ITucsonProvider interfaces
	/// </summary>
	public static class TucsonProviderFactory
	{
		/// <summary>
		/// Sets the provider creator method for the type
		/// </summary>
		/// <typeparam name="T">The type of the provider</typeparam>
		/// <typeparam name="TSettings">The type of the settings</typeparam>
		/// <param name="creator">The method to create the provider</param>
		public static void SetProviderCreator<T, TSettings>(Func<TSettings, T> creator)
			where T : class, ITucsonProvider
			where TSettings : ITucsonSettings
		{
			// this will need to be written if you want to use factory
			//Configuration.ProviderWithSettingsFactory.SetProviderCreator(creator);
		}

		/// <summary>
		/// Creates a new provider.  Be sure to Dispose() the result of this method in a using() statement
		/// </summary>
		/// <typeparam name="T">The type of the provider</typeparam>
		/// <typeparam name="TSettings">The type of the settings</typeparam>
		/// <returns></returns>
		public static T CreateProvider<T, TSettings>(TSettings settings)
			where T: class, ITucsonProvider
			where TSettings : ITucsonSettings
		{
			// this will need to be written if you want to use factory
			return null;//Configuration.ProviderWithSettingsFactory.CreateProvider<T, TSettings>(settings);
		}
	}
}
