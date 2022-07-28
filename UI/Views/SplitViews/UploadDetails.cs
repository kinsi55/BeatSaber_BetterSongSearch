using BeatSaberMarkupLanguage.Attributes;
using BetterSongSearch.Util;
using HMUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;

namespace BetterSongSearch.UI.SplitViews {
	class UploadDetails {
		public static readonly UploadDetails instance = new UploadDetails();
		UploadDetails() { }

		[UIComponent("selectedCharacteristics")] readonly TextMeshProUGUI selectedCharacteristics = null;
		[UIComponent("selectedSongKey")] readonly TextMeshProUGUI selectedSongKey = null;
		[UIComponent("selectedSongDescription")] readonly CurvedTextMeshPro selectedSongDescription = null;
		[UIComponent("selectedRating")] readonly TextMeshProUGUI selectedRating = null;
		//[UIComponent("selectedDownloadCount")] TextMeshProUGUI selectedDownloadCount = null;
		[UIComponent("songDetailsLoading")] readonly ImageView songDetailsLoading = null;

		public async void Populate(SongSearchSong selectedSong) {
			selectedCharacteristics.text = String.Join(", ", selectedSong.detailsSong.difficulties.GroupBy(x => x.characteristic).Select(x => $"{x.Count()}x {x.Key}"));
			selectedSongKey.text = selectedSong.detailsSong.key;
			//selectedDownloadCount.text = selectedSong.detailsSong.downloadCount.ToString("N0");
			selectedRating.text = selectedSong.detailsSong.rating.ToString("0.0%");
			selectedSongDescription.text = "Loading...";

			songDetailsLoading.gameObject.SetActive(true);

			var desc = await Task.Run(async () => {
				try {
					return await BSSFlowCoordinator.assetLoader.GetSongDescription(selectedSong.detailsSong.key, BSSFlowCoordinator.closeCancelSource.Token);
				} catch { }
				return "Failed to load description";
			});

			songDetailsLoading.gameObject.SetActive(false);
			selectedSongDescription.text = desc;
			selectedSongDescription.gameObject.SetActive(false);
			selectedSongDescription.gameObject.SetActive(true);
		}
	}
}
