using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSongSearch.Util {
	static class BSMLStuff {
		public static IEnumerator MergeSliders(GameObject container, bool constrictValuesMinMax = true) {
			yield return null;
			foreach(var x in container.GetComponentsInChildren<CurvedTextMeshPro>().Where(x => x.text == "MERGE_TO_PREV")) {
				yield return new WaitForEndOfFrame();
				var ourContainer = x.transform.parent;
				var prevContainer = ourContainer.parent.GetChild(ourContainer.GetSiblingIndex() - 1);

				(prevContainer.Find("Title").transform as RectTransform).offsetMax = new Vector2(-40, 0);
				(prevContainer.Find("BSMLSlider").transform as RectTransform).offsetMax = new Vector2(-20, 0);
				(ourContainer.Find("BSMLSlider").transform as RectTransform).offsetMin = new Vector2(-20, 0);
				ourContainer.position = prevContainer.position;

				var minTimeSlider = prevContainer.GetComponentInChildren<TextSlider>();
				var maxTimeSlider = ourContainer.GetComponentInChildren<TextSlider>();

				if(minTimeSlider == null || maxTimeSlider == null)
					yield break;

				maxTimeSlider.valueSize = minTimeSlider.valueSize /= 2.1f;

				ourContainer.GetComponentInChildren<LayoutElement>().ignoreLayout = true;
				x.text = "";

				// I tried to get this to work for an hour, cba for now
				//if(!constrictValuesMinMax)
				//	continue;

				//var minTimeSliderBsml = minTimeSlider.GetComponentInParent<SliderSetting>();
				//var maxTimeSliderBsml = maxTimeSlider.GetComponentInParent<SliderSetting>();

				//var originalMinMax = minTimeSlider.maxValue;
				//var originalMaxMin = maxTimeSlider.minValue;

				//minTimeSlider.normalizedValueDidChangeEvent += (slider, value) => {
				//	var m = ReflectionUtil.GetField<float, TextSlider>(maxTimeSlider, "_normalizedValue");
				//	var limit = Math.Min(originalMaxMin, maxTimeSlider.value);
				//	ReflectionUtil.SetField((RangeValuesTextSlider)maxTimeSlider, "_minValue", limit);
				//	maxTimeSlider.value = value;
				//};

				//maxTimeSlider.normalizedValueDidChangeEvent += (slider, value) => {
				//	var m = ReflectionUtil.GetField<float, TextSlider>(minTimeSlider, "_normalizedValue");
				//	var limit = Math.Max(originalMinMax, maxTimeSlider.value);
				//	ReflectionUtil.SetField((RangeValuesTextSlider)slider, "_maxValue", limit);
				//	minTimeSlider.value = value;
				//};
			}
		}

		public static BSMLParserParams InitSplitView(ref BSMLParserParams pparams, GameObject targetGameObject, object host, string viewName = null) {
			if(pparams != null)
				return pparams;

			if(viewName == null)
				viewName = host.GetType().Name;

			return pparams = BSMLParser.Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), $"BetterSongSearch.UI.Views.SplitViews.{viewName}.bsml"), targetGameObject, host);
		}

		static GameObject scrollBar = null;

		public static GameObject GetScrollbarForTable(GameObject table, Transform targetContainer) {
			if(scrollBar == null)
				scrollBar = Resources.FindObjectsOfTypeAll<VerticalScrollIndicator>().FirstOrDefault(x => x.enabled)?.transform.parent?.gameObject;

			if(scrollBar == null)
				return null;

			var sw = table.GetComponentInChildren<ScrollView>();

			if(sw == null)
				return null;

			var listScrollBar = GameObject.Instantiate(scrollBar, targetContainer, false);
			listScrollBar.SetActive(true);
			var vsi = listScrollBar.GetComponentInChildren<VerticalScrollIndicator>(true);

			sw._verticalScrollIndicator = vsi;

			var buttoneZ = listScrollBar.GetComponentsInChildren<NoTransitionsButton>(true).OrderByDescending(x => x.gameObject.name == "UpButton").ToArray();
			if(buttoneZ.Length == 2) {
				sw._pageUpButton = buttoneZ[0];
				sw._pageDownButton = buttoneZ[1];

				buttoneZ[0].onClick.AddListener(sw.PageUpButtonPressed);
				buttoneZ[1].onClick.AddListener(sw.PageDownButtonPressed);
			}

			// I dont know WHY I need do do this, but if I dont the scrollbar wont work with the added modal.
			foreach(Transform x in listScrollBar.transform) {
				foreach(var y in x.GetComponents<Behaviour>())
					y.enabled = true;
			}

			sw.Update();
			sw.gameObject.AddComponent<RefreshScrolbarOnFirstLoad>();

			return scrollBar;
		}

		class RefreshScrolbarOnFirstLoad : MonoBehaviour {
			void OnEnable() => StartCoroutine(dorefresh());

			IEnumerator dorefresh() {
				yield return null;
				var sv = gameObject.GetComponent<ScrollView>();

				if(sv == null || sv._verticalScrollIndicator == null)
					yield break;

				sv._verticalScrollIndicator.RefreshHandle();
			}
		}
	}
}
