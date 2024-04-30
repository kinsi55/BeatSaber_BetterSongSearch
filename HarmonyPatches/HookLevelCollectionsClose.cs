using HarmonyLib;

namespace BetterSongSearch.HarmonyPatches {
	[HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridView), nameof(AnnotatedBeatmapLevelCollectionsGridView.CloseLevelCollection))]
	static class AnnotatedBeatmapLevelCollectionsGridViewPatch {
		static bool Prefix(AnnotatedBeatmapLevelCollectionsGridView __instance) => __instance._gridView.columnCount != 0;
	}
}
