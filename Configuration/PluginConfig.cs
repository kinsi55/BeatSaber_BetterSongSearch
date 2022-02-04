using BetterSongSearch.UI;
using IPA.Config.Stores;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace BetterSongSearch {
	internal class PluginConfig {
		public static PluginConfig Instance;
		public virtual bool returnToBssFromSolo { get; set; } = false;
		public virtual bool smallerFontSize { get; set; } = false;
		public virtual string downloadUrlOverride { get; set; } = "";
		public virtual string apiUrlOverride { get; set; } = "";

		/// <summary>
		/// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
		/// </summary>
		public virtual void OnReload() {
			// Do stuff after config is read from disk.
		}

		/// <summary>
		/// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
		/// </summary>
		public virtual void Changed() {
			// Do stuff when the config is changed.
			if(BSSFlowCoordinator.songListView && BSSFlowCoordinator.songListView.songList)
				BSSFlowCoordinator.songListView.songList.ReloadData();
		}

		/// <summary>
		/// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
		/// </summary>
		public virtual void CopyFrom(PluginConfig other) {
			// This instance's members populated from other
		}
	}
}