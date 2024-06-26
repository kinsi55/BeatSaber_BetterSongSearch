using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BetterSongSearch.Configuration;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSongSearch.UI.SplitViews {
	class GenrePicker {
		public static readonly GenrePicker instance = new GenrePicker();
		GenrePicker() { }

		class FilterPresetRow {
			public enum State {
				None,
				Include,
				Exclude
			}

			public readonly ulong value;
			public readonly string name;
			public readonly string mappedName;
			public State selectionState { get; private set; } = State.None;
			//[UIComponent("label")] readonly TextMeshProUGUI label = null;
			[UIComponent("excludeButton")] readonly ClickableText excludeButton = null;
			[UIComponent("includeButton")] readonly ClickableText includeButton = null;

			public FilterPresetRow(KeyValuePair<string, ulong> tag) {
				this.value = tag.Value;
				this.name = tag.Key;

				this.mappedName = FilterOptions.FormatBeatSaverTag(this.name);

				var count = 0;

				//TODO: This is unoptimized as fuck
				for(var i = BSSFlowCoordinator.songDetails.songs.Length; i-- > 0;) {
					ref var song = ref BSSFlowCoordinator.songDetails.songs[i];

					if((song.tags & this.value) != 0)
						count++;
				}

				this.mappedName += $" ({count})";

				this.selectionState =
					(FilterView.currentFilter._mapGenreBitfield & this.value) != 0 ? State.Include :
					(FilterView.currentFilter._mapGenreExcludeBitfield & this.value) != 0 ? State.Exclude :
					State.None;
			}

			public void SetSelectionState(State newState) {
				this.selectionState = newState;
				Refresh(true, true);
			}

			[UIAction("ExcludeGenre")]
			public void ExcludeGenre() {
				SetSelectionState(State.Exclude);
			}

			[UIAction("IncludeGenre")]
			public void IncludeGenre() {
				SetSelectionState(selectionState == State.Include ? State.None : State.Include);
			}

			[UIAction("refresh-visuals")]
			public void Refresh(bool selected, bool highlighted) {
				includeButton.fontStyle = 
					selectionState == State.Exclude ? 
					FontStyles.Strikethrough : 
					FontStyles.Normal;

				includeButton.color = new UnityEngine.Color(
					selectionState == State.Exclude ? 1f : .8f,
					selectionState == State.Include ? 1f : .7f,
					.8f,
					highlighted ? 1f : 0.8f
				);

				excludeButton.gameObject.SetActive(highlighted && selectionState != State.Exclude);
			}
		}

		[UIAction("#post-parse")]
		void Parsed() {
			SongDetailsCache.SongDetailsContainer.dataAvailableOrUpdated += Reload;
		}


		[UIComponent("genreList")] readonly CustomCellListTableData genreList = null;
		internal void Reload() {
			genreList.data = BSSFlowCoordinator.songDetails.tags
				.Where(x => !FilterOptions.mapStyles.Contains(x.Key))
				.OrderBy(x => x.Key)
				.Select(x => new FilterPresetRow(x)).ToList<object>();

			genreList.tableView.ReloadData();
			genreList.tableView.ClearSelection();
		}

		void GenreSelected(object _, FilterPresetRow row) { }

		void SelectGenre() {
			var selectedGenres = new List<string>();
			var excludedGenres = new List<string>();
			foreach(var _entry in genreList.data) {
				var fpr = (FilterPresetRow)_entry;

				if(fpr.selectionState == FilterPresetRow.State.Include)
					selectedGenres.Add(fpr.name);
				else if(fpr.selectionState == FilterPresetRow.State.Exclude)
					excludedGenres.Add(fpr.name);
			}

			BSSFlowCoordinator.filterView.SetGenreFilter(selectedGenres, excludedGenres);
		}

		void ClearGenre() {
			BSSFlowCoordinator.filterView.SetGenreFilter(null, null);
		}
	}
}
