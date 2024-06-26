using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.Configuration;
using BetterSongSearch.Util;
using HarmonyLib;
using HMUI;
using IPA.Utilities;
using SongDetailsCache.Structs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BetterSongSearch.UI {

	[HotReload(RelativePathToLayout = @"Views\FilterView.bsml")]
	[ViewDefinition("BetterSongSearch.UI.Views.FilterView.bsml")]
	class FilterView : BSMLAutomaticViewController, INotifyPropertyChanged {
		public static List<DateTime> hideOlderThanOptions { get; private set; } = BuildList();

		static List<DateTime> BuildList() {
			var hideOlderThanOptions = new List<DateTime>();

			for(var x = new DateTime(2018, 5, 1); x < DateTime.Now; x = x.AddMonths(1))
				hideOlderThanOptions.Add(x);

			return hideOlderThanOptions;
		}

		public static readonly FilterOptions currentFilter = new FilterOptions();

		[UIComponent("filterbarContainer")] Transform filterbarContainer = null;
		//[UIComponent("modsRequirementDropdown")] DropdownWithTableView _modsRequirementDropdown = null;

		IEnumerator FixupScrollpanel() {
			yield return null;

			var beatsaverFilterScroller = gameObject.GetComponentInChildren<BSMLScrollView>().transform;

			((RectTransform)beatsaverFilterScroller.Find("Viewport").transform).sizeDelta = new Vector2(-12, -6);

			foreach(var input in 
				beatsaverFilterScroller.Find("Viewport/BSMLScrollViewContent/BSMLScrollViewContentContainer/BSMLVerticalLayoutGroup")
				.GetComponentsInChildren<Touchable>()
			) {
				RectTransform t = (RectTransform)input.transform;

				if(input.name == "IncButton" || input.name == "DropDownButton") {
					t = (RectTransform)t.parent;
					t.Find("DecButton")?.gameObject.SetActive(false);
				}

				if(t.sizeDelta.x > 30)
					t.sizeDelta = new Vector2(30, 0);
			}
		}

		[UIAction("#post-parse")]
		void Parsed() {
			currentFilter.hideOlderThanSlider.slider.maxValue = hideOlderThanOptions.Count - 1;

			((RectTransform)gameObject.transform).offsetMax = new Vector2(20, 22);

			StartCoroutine(BSMLStuff.MergeSliders(gameObject));
			StartCoroutine(FixupScrollpanel());

			// I hate BSML some times
			var y = GetComponentsInChildren<DropDownListSetting>().Where(x => x.associatedValue.MemberName == "mods").FirstOrDefault();
			if(y != null) {
				var m = ReflectionUtil.GetField<ModalView, DropdownWithTableView>(
					y.GetComponent<DropdownWithTableView>()
				, "_modalView");
				((RectTransform)m.transform).pivot = new Vector2(0.5f, 0.3f);
			}

			// This is garbage
			foreach(var x in GetComponentsInChildren<Backgroundable>().Select(x => x.GetComponent<ImageView>())) {
				if(!x || x.color0 != Color.white || x.sprite.name != "RoundRect10")
					continue;

				ReflectionUtil.SetField(x, "_skew", 0f);
				x.overrideSprite = null;
				x.SetImage("#RoundRect10BorderFade");
				x.color = new Color(0, 0.7f, 1f, 0.4f);
			}

			foreach(var x in filterbarContainer.GetComponentsInChildren<ImageView>().Where(x => x.gameObject.name == "Underline"))
				x.SetImage("#RoundRect10BorderFade");
		}

		internal void ClearFilters() => SetFilter();

		internal void SetFilter(FilterOptions filter = null) {
			filter ??= new FilterOptions();
			foreach(var x in AccessTools.GetDeclaredProperties(typeof(FilterOptions))) {
				if(!x.CanWrite || x.Name[0] == '_')
					continue;

				x.SetValue(currentFilter, x.GetValue(filter));
			}

			SetGenreFilter(null, null);
			currentFilter.NotifyPropertiesChanged();
			/*
			 * This is a massive hack and I have NO IDEA why I need to do this. If I dont do this, or
			 * the FilterSongs() method NEVER ends up being called, not even if I manually invoke
			 * limitedUpdateData.Call[NextFrame]() OR EVEN BSSFlowCoordinator.FilterSongs() DIRECTLY
			 * Seems like there is SOMETHING broken with how input changes are handled, something to do
			 * with nested coroutines or whatever. I have no idea. For now I spent enough time trying to fix this
			 */
			currentFilter.hideOlderThanSlider.onChange.Invoke(currentFilter.hideOlderThanSlider.Value);
		}

		BSMLParserParams presetsViewParams = null;
		[UIAction("ShowPresets")] void ShowPresets() {
			BSMLStuff.InitSplitView(ref presetsViewParams, gameObject, SplitViews.Presets.instance).EmitEvent("OpenPresets");

			SplitViews.Presets.instance.ReloadPresets();
		}


		BSMLParserParams genreViewParams = null;
		[UIAction("ShowGenrePicker")]
		void ShowGenrePicker() {
			BSMLStuff.InitSplitView(ref genreViewParams, gameObject, SplitViews.GenrePicker.instance).EmitEvent("OpenGenreModal");

			SplitViews.GenrePicker.instance.Reload();
		}

		[UIComponent("genrePickButton")] internal NoTransitionsButton genrePickButton = null;
		internal void SetGenreFilter(List<string> includedGenres, List<string> excludedGenres) {
			currentFilter.mapGenreString = includedGenres == null ? "" : string.Join(",", includedGenres);
			currentFilter.mapGenreExcludeString = excludedGenres == null ? "" : string.Join(",", excludedGenres);

			var genrefilter = "Any";

			if(includedGenres?.Count > 0 || excludedGenres?.Count > 0)
				genrefilter = $"{includedGenres?.Count ?? 0} Incl., {excludedGenres?.Count ?? 0} Excl.";

			genrePickButton.GetComponentInChildren<CurvedTextMeshPro>().text = genrefilter;

			FilterOptions.UpdateData();
		}


		#region filters
		static bool requiresScore => (currentFilter.existingScore == (string)FilterOptions.scoreFilterOptions[2]) || SongListController.selectedSortMode == "Worst local score";

		static readonly IReadOnlyDictionary<object, MapMods> funnyMapThing = Enumerable.Range(0, 4)
			.ToDictionary(x => FilterOptions.modOptions[x + 1], x => (MapMods)(1 << x));

		static readonly IReadOnlyDictionary<string, RankedStates> funnyMapThing2 = Enumerable.Range(0, 4)
			.ToDictionary(x => (string)FilterOptions.rankedFilterOptions[x + 1], x => (RankedStates)(1 << x));

		public bool DifficultyCheck(in SongDifficulty diff) {
			if(currentFilter.difficulty_int != -1 && currentFilter.difficulty_int != (int)diff.difficulty)
				return false;

			if(currentFilter.characteristic_int != -1 && currentFilter.characteristic_int != (int)diff.characteristic)
				return false;

			if(diff.njs < currentFilter.minimumNjs || diff.njs > currentFilter.maximumNjs)
				return false;

			if(currentFilter.rankedState != (string)FilterOptions.rankedFilterOptions[0]) {
				if(!diff.song.rankedStates.HasFlag(funnyMapThing2[currentFilter.rankedState]))
					return false;
			}

			if(currentFilter.mods != (string)FilterOptions.modOptions[0]) {
				if((diff.mods & funnyMapThing[currentFilter.mods]) == 0)
					return false;
			}

			if(diff.song.songDurationSeconds > 0) {
				var nps = diff.notes / (float)diff.song.songDurationSeconds;

				if(nps < currentFilter.minimumNps || nps > currentFilter.maximumNps)
					return false;
			}

			return true;
		}

		public bool SearchDifficultyCheck(SongSearchSong.SongSearchDiff diff) {
			if(currentFilter.existingScore != (string)FilterOptions.scoreFilterOptions[0] || requiresScore) {
				if(diff.CheckHasScore() != requiresScore)
					return false;
			}

			var star = -1f;

			if(currentFilter.maximumStars != FilterOptions.STAR_FILTER_MAX) {
				star = diff.GetStars();

				if(star > currentFilter.maximumStars)
					return false;
			}

			if(currentFilter.minimumStars != 0f) {
				if(star == -1)
					star = diff.GetStars();

				if(star < currentFilter.minimumStars)
					return false;
			}

			return true;
		}

		public bool SongCheck(in Song song) {
			if(song.uploadTime < currentFilter.hideOlderThan)
				return false;

			if(currentFilter.rankedState != (string)FilterOptions.rankedFilterOptions[0]) {
				if(!song.rankedStates.HasFlag(funnyMapThing2[currentFilter.rankedState]))
					return false;
			}

			const float oneSixtythInverse = 1f / 60;

			if(song.songDurationSeconds > 0f) {
				var x = song.songDurationSeconds * oneSixtythInverse;

				if(x < currentFilter.minimumSongLength || x > currentFilter.maximumSongLength)
					return false;
			}

			var voteCount = song.downvotes + song.upvotes;

			if(voteCount < currentFilter.minimumVotes)
				return false;

			if(currentFilter.minimumRating > 0f && (currentFilter.minimumRating > song.rating || voteCount == 0))
				return false;

			if(currentFilter.onlyCuratedMaps && (song.uploadFlags & UploadFlags.Curated) == 0)
				return false;

			if(currentFilter.onlyVerifiedMappers && (song.uploadFlags & UploadFlags.VerifiedUploader) == 0)
				return false;

			if(currentFilter._mapStyleBitfield != 0 && (song.tags & currentFilter._mapStyleBitfield) == 0)
				return false;

			if(currentFilter._mapGenreBitfield != 0 && (song.tags & currentFilter._mapGenreBitfield) == 0)
				return false;

			if((song.tags & currentFilter._mapGenreExcludeBitfield) != 0)
				return false;

			return true;
		}

		public bool SearchSongCheck(SongSearchSong song) {
			if(currentFilter.existingSongs != (string)FilterOptions.downloadedFilterOptions[0]) {
				if(SongCore.Collections.songWithHashPresent(song.hash) == (currentFilter.existingSongs == (string)FilterOptions.downloadedFilterOptions[2]))
					return false;
			}

			if(currentFilter.existingScore != (string)FilterOptions.scoreFilterOptions[0] || requiresScore) {
				if(song.CheckHasScore() != requiresScore)
					return false;
			}

			if(currentFilter.uploaders.Count != 0) {
				if(currentFilter.uploaders.Contains(song.uploaderNameLowercase)) {
					if(currentFilter.uploadersBlacklist)
						return false;
				} else if(!currentFilter.uploadersBlacklist) {
					return false;
				}
			}

			return true;
		}
		#endregion


		[UIComponent("sponsorsText")] CurvedTextMeshPro sponsorsText = null;
		void OpenSponsorsLink() => Process.Start("https://github.com/sponsors/kinsi55");
		async void OpenSponsorsModal() {
			sponsorsText.text = "Loading...";
			var desc = await Task.Run(() => {
				try {
					return (new WebClient()).DownloadString("http://kinsi.me/sponsors/bsout.php");
				} catch { }
				return "Failed to load";
			});

			sponsorsText.text = desc;
			// There is almost certainly a better way to update / correctly set the scrollbar size...
			sponsorsText.gameObject.SetActive(false);
			sponsorsText.gameObject.SetActive(true);
		}


		public static readonly RatelimitCoroutine limitedUpdateData = new RatelimitCoroutine(BSSFlowCoordinator.FilterSongs, 0.1f);

		readonly string version = $"BetterSongSearch v{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)} by Kinsi55";
		[UIComponent("datasetInfoLabel")] private TextMeshProUGUI _datasetInfoLabel = null;
		public TextMeshProUGUI datasetInfoLabel => _datasetInfoLabel;
	}
}
