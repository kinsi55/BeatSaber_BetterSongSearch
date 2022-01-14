using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.UI.SplitViews {
	class PlaylistCreation {
		public static readonly PlaylistCreation instance = new PlaylistCreation();
		PlaylistCreation() { }

		[UIComponent("playlistSongsCountSlider")] internal SliderSetting playlistSongsCountSlider = null;
		[UIComponent("playlistName")] private StringSetting playlistName = null;

		[UIAction("#post-parse")]
		void Parsed() {

			// BSML / HMUI my beloved
			ReflectionUtil.SetField(playlistName.modalKeyboard.modalView, "_animateParentCanvas", false);
		}
	}
}
