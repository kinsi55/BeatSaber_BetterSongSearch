using HarmonyLib;
using HMUI;
using IPA.Utilities;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSongSearch.Util {
	static class BSMLStuff {
		public static void UnleakTable(GameObject gameObject) {
			foreach(var x in gameObject.GetComponentsInChildren<Touchable>(true).Where(x => !x.gameObject.activeSelf)) {
				var l = x.gameObject;

				GameObject.DestroyImmediate(x);
				GameObject.DestroyImmediate(l.GetComponent<CanvasRenderer>());
				GameObject.DestroyImmediate(l);
			}
		}

		public static IEnumerator MergeSliders(GameObject container) {
			yield return 0;
			foreach(var x in container.GetComponentsInChildren<CurvedTextMeshPro>().Where(x => x.text == "MERGE_TO_PREV")) {
				yield return new WaitForEndOfFrame();
				var ourContainer = x.transform.parent;
				var prevContainer = ourContainer.parent.GetChild(ourContainer.GetSiblingIndex() - 1);

				(prevContainer.Find("BSMLSlider").transform as RectTransform).offsetMax = new Vector2(-20, 0);
				(ourContainer.Find("BSMLSlider").transform as RectTransform).offsetMin = new Vector2(-20, 0);
				ourContainer.position = prevContainer.position;

				prevContainer.GetComponentInChildren<TimeSlider>().valueSize /= 2.1f;
				ourContainer.GetComponentInChildren<TimeSlider>().valueSize = 9;

				ourContainer.GetComponentInChildren<LayoutElement>().ignoreLayout = true;
				x.text = "";
			}
		}

		public static GameObject GetScrollbarForTable(GameObject table, Transform targetContainer) {
			var scrollBar = Resources.FindObjectsOfTypeAll<VerticalScrollIndicator>().FirstOrDefault(x => x.enabled)?.transform.parent?.gameObject;

			if(scrollBar == null)
				return null;

			var sw = table.GetComponentInChildren<ScrollView>();

			if(sw == null)
				return null;

			var listScrollBar = GameObject.Instantiate(scrollBar, targetContainer, false);
			listScrollBar.SetActive(true);
			var vsi = listScrollBar.GetComponentInChildren<VerticalScrollIndicator>();

			ReflectionUtil.SetField(sw, "_verticalScrollIndicator", vsi);

			var buttoneZ = listScrollBar.GetComponentsInChildren<NoTransitionsButton>().OrderByDescending(x => x.gameObject.name == "UpButton").ToArray();
			if(buttoneZ.Length == 2) {
				ReflectionUtil.SetField(sw, "_pageUpButton", (Button)buttoneZ[0]);
				ReflectionUtil.SetField(sw, "_pageDownButton", (Button)buttoneZ[1]);

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
				yield return 0;
				var sv = gameObject.GetComponent<ScrollView>();

				if(sv == null)
					yield break;
				ReflectionUtil.GetField<VerticalScrollIndicator, ScrollView>(sv, "_verticalScrollIndicator")?.RefreshHandle();
			}
		}
	}
}
