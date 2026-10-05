#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Extension
{
	/// <summary>
	/// Use for all Maintainer extensions in order to automatically implement IMaintainerExtension
	/// </summary>
	public abstract class MaintainerExtension : IMaintainerExtension
	{
		protected abstract bool Enabled { get; set; }
		private string Id { get; set; }

		bool IMaintainerExtension.External { get; set; }
		
		bool IMaintainerExtension.Enabled
		{
			get => Enabled;
			set => Enabled = value;
		}
		
		string IMaintainerExtension.Id => Id ?? (Id = GetId(this));

		internal static string GetId(IMaintainerExtension instance)
		{
			return instance.GetType().Name;
		}
		
		internal static string GetId<T>() where T : IMaintainerExtension
		{
			return typeof(T).Name;
		}
	}
}