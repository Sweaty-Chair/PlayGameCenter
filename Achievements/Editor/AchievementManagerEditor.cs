using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweatyChair
{

	[CustomEditor(typeof(AchievementManager))]
	public class AchievementManagerEditor : Editor
	{

		private const string FILENAME_ACHIEVEMENT = "Achievement.cs";
		private const string DIR_MODULE_PATH = "/SweatyChair/Achievements/";

		private string modulePath { get { return Application.dataPath + DIR_MODULE_PATH; } }

		private static bool _isCompiling = false;

		private bool _confirmDelete = false;
		private bool[] _showAchievements;

		private AchievementManager _am {
			get { return target as AchievementManager; }
		}

		private int _cachedCurIndex = -1;

		private int _curIndex {
			get {
				if (_cachedCurIndex == -1)
					_cachedCurIndex = EditorPrefs.GetInt("AchievementManagerEditorAchievementIndex");
				return _cachedCurIndex;
			}
			set {
				_cachedCurIndex = value;
				EditorPrefs.SetInt("AchievementManagerEditorAchievementIndex", value);
			}
		}

		private Achievement _curAchievement {
			get { return (Achievement)_curIndex; }
		}

		private string _updatedAchievement = "";

		public override void OnInspectorGUI()
		{
			if (_isCompiling) {

				GUILayout.Label("Wait for compiling finish...");

			} else if (_confirmDelete) {

				// Show the confirmation dialog
				GUILayout.Label(string.Format("Sure to delete achievement '{0}'?", _curAchievement));
				EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

				GUILayout.BeginHorizontal();
				{
					GUI.backgroundColor = Color.green;

					if (GUILayout.Button("Cancel"))
						_confirmDelete = false;

					GUI.backgroundColor = Color.red;

					if (GUILayout.Button("Delete")) {
						DeleteAchievementId(_curAchievement.ToString());
						_confirmDelete = false;
					}

					GUI.backgroundColor = Color.white;
				}
				GUILayout.EndHorizontal();

			} else {

				OnAchievementGUI();

				EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

				OnManagerGUI();
			}
		}

		private void OnAchievementGUI()
		{
			// New
			GUI.backgroundColor = Color.green;
			if (GUILayout.Button("New Achievement")) {

				int lastIdIndex = EnumUtils.GetCount<Achievement>() - 1;
				string newIdStr = lastIdIndex >= 0 ? ((Achievement)lastIdIndex).ToString() + "_Clone" : "New Achievement";
				AddAchievementId(newIdStr);

			}
			GUI.backgroundColor = Color.white;

			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			// Navigation
			GUILayout.BeginHorizontal();
			{
				bool canGoPrev = true;
				if (_curIndex <= 0) {
					GUI.color = Color.grey;
					canGoPrev = false;
				}
				if (GUILayout.Button("<<")) {
					if (canGoPrev) {
						_confirmDelete = false;
						_updatedAchievement = "";
						_curIndex--;
					}
				}
				GUI.color = Color.white;

				_curIndex = EditorGUILayout.IntField(_curIndex + 1, GUILayout.Width(40)) - 1;
				GUILayout.Label("/ " + EnumUtils.GetCount<Achievement>(), GUILayout.Width(40));

				bool canGoNext = true;
				if (_curIndex >= (EnumUtils.GetCount<Achievement>() - 1)) {
					GUI.color = Color.grey;
					canGoNext = false;
				}
				if (GUILayout.Button(">>")) {
					if (canGoNext) {
						_confirmDelete = false;
						_updatedAchievement = "";
						_curIndex++;
					}
				}
				GUI.color = Color.white;
			}
			GUILayout.EndHorizontal();

			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			// Achievement Group Info

			// ID - use SetNextControlName to avoid it keep update and write to script everytime a key is pressed
			GUI.SetNextControlName("ID");

			if (string.IsNullOrEmpty(_updatedAchievement) && !string.IsNullOrEmpty(_curAchievement.ToString()))
				_updatedAchievement = _curAchievement.ToString();

			_updatedAchievement = EditorGUILayout.TextField(new GUIContent("ID", "Used to call get the achievement info and report the achievement."), _updatedAchievement);

			if (GUI.GetNameOfFocusedControl() == "ID" && Event.current.isKey && Event.current.keyCode == KeyCode.Return) {
				if (_updatedAchievement != _curAchievement.ToString() && !AchievementIdExists(_updatedAchievement))
					ReplaceAchievementId(_curAchievement.ToString(), _updatedAchievement);
			}

			if (_updatedAchievement != _curAchievement.ToString()) {
				if (AchievementIdExists(_updatedAchievement))
					EditorGUILayout.HelpBox("Achievement already exists", MessageType.Error);
				else
					EditorGUILayout.HelpBox("Press Enter to Apply", MessageType.Warning);
			}

			// Check the length of achievements is valid
			if (_am.achievementGroupInfos == null || _am.achievementGroupInfos.Length <= _curIndex)
				ValidateAchievementInfos();
			AchievementGroupInfo groupInfo = _am.achievementGroupInfos[_curIndex];

			if (groupInfo.isInGame) {

				// Name
				string name = EditorGUILayout.TextField(new GUIContent("Name", "Name of the achievement group, used for in-game UI only."), groupInfo.name);
				if (name != groupInfo.name) {
					Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Group Name");
					groupInfo.name = name;
				}
				// Description
				string description = EditorGUILayout.TextField(new GUIContent("Description", "Description of the achievement group, used for in-game UI only."), groupInfo.description);
				if (description != groupInfo.description) {
					Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Group Description");
					groupInfo.description = description;
				}

			}

			// Is incremental
			bool isIncrement = EditorGUILayout.Toggle(new GUIContent("Incremental", "If incremental, current count of the achievement is saved and increment number is input each time; Otherwise, the full count should be input."), groupInfo.isIncrement);
			if (isIncrement != groupInfo.isIncrement) {
				Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Group Is Incremental");
				groupInfo.isIncrement = isIncrement;
			}

			// Has in-game rewards
			bool isInGame = EditorGUILayout.Toggle(new GUIContent("In-Game", "If this achievement is shown / can be rewarded in-game too, this will use GameSave for saving progress count; otherwise it use PlayerPrefs."), groupInfo.isInGame);
			if (isInGame != groupInfo.isInGame) {
				Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Group Is In-Game");
				groupInfo.isInGame = isInGame;
			}

			// Total of sub-achivements
			int subAchievementsCount = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Total Achievements", "The total achievements of this achievement category."), groupInfo.achievementInfos.Length)); // At least 1
			if (subAchievementsCount != groupInfo.achievementInfos.Length) {
				Undo.RegisterCompleteObjectUndo(_am, "Edit Total Achievements");
				Array.Resize<AchievementInfo>(ref groupInfo.achievementInfos, subAchievementsCount);
			}

			if (_showAchievements == null || _showAchievements.Length != subAchievementsCount)
				_showAchievements = _showAchievements.Resize<bool>(subAchievementsCount, true);

			EditorGUI.indentLevel++;

			// Achievement Infos
			for (int i = 0; i < groupInfo.achievementInfos.Length; i++) {

				GUILayout.BeginHorizontal();
				{
					_showAchievements[i] = EditorGUILayout.Foldout(_showAchievements[i], string.Format("Achievement {0}", i + 1), true);
					EditorGUI.BeginDisabledGroup(true);
					if (groupInfo.IsCompleted(i))
						EditorGUILayout.Toggle(true);
					EditorGUI.EndDisabledGroup();
				}
				GUILayout.EndHorizontal();

				if (_showAchievements[i]) {

					EditorGUI.indentLevel++;

					AchievementInfo info = groupInfo.achievementInfos[i];

					if (groupInfo.isInGame) {

						// Name
						string aName = EditorGUILayout.TextField(new GUIContent("Name", "Name of the achievement, used for in-game UI only."), info.name);
						if (aName != info.name) {
							Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Name");
							groupInfo.achievementInfos[i].name = aName;
						}

						// Description
						string aDescription = EditorGUILayout.TextField(new GUIContent("Description", "Description of the achievement, used for in-game UI only."), info.description);
						if (aDescription != info.name) {
							Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Description");
							groupInfo.achievementInfos[i].description = aDescription;
						}

					}

					// Requirement
					int requirement = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Requirement", "Required count for completing this achievement."), info.requirement)); // At least 0
					if (requirement != info.requirement) {
						Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Requirement");
						groupInfo.achievementInfos[i].requirement = requirement;
					}

					// iOS ID
					string iOSId = EditorGUILayout.TextField(new GUIContent("iOS ID", "Achievement ID setup in iTunesConnect console, format as: \"com.sweatychair.gamename.achievementname\"."), info.iOSId);
					if (iOSId != info.iOSId) {
						Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement iOS ID");
						groupInfo.achievementInfos[i].iOSId = iOSId;
					}

					// Android ID
					string androidId = EditorGUILayout.TextField(new GUIContent("Android ID", "Achievement ID setup in Play Games console, format as: \"CgkI9snButIFEAIQAw\"."), info.androidId);
					if (androidId != info.androidId) {
						Undo.RegisterCompleteObjectUndo(_am, "Edit Achievement Android ID");
						groupInfo.achievementInfos[i].androidId = androidId;
					}

					if (groupInfo.isInGame) {

						// Total rewards
						int rewardCount = EditorGUILayout.IntField(new GUIContent("Total Reward Items", "The rewarding item after completing it."), info.reward == null ? 0 : info.reward.count);
						if (rewardCount != info.reward.count) {
							Undo.RegisterCompleteObjectUndo(_am, "Edit Total Reward Items");
							groupInfo.achievementInfos[i].reward.count = rewardCount;
						}

						EditorGUI.indentLevel++;

						// Rewards
						for (int j = 0; j < info.reward.count; j++) {

							EditorGUILayout.LabelField(string.Format("Reward {0}", j + 1));

							EditorGUI.indentLevel++;

							// Rewards types
							ItemType itemType = (ItemType)EditorGUILayout.EnumPopup(new GUIContent("Types", "Reward item type."), info.reward.items[j].itemType);
							if (itemType != info.reward.items[j].itemType) {
								Undo.RegisterCompleteObjectUndo(_am, "Edit Reward Types");
								groupInfo.achievementInfos[i].reward.items[j].SetItemType(itemType);
							}

							// Rewards amount
							int amount = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Amount", "Reward item amount."), info.reward.items[j].amount));
							if (amount != info.reward.items[j].amount) {
								Undo.RegisterCompleteObjectUndo(_am, "Edit Reward Amount");
								groupInfo.achievementInfos[i].reward.items[j].SetAmount(amount);
							}

							// Rewards id
							int id = EditorGUILayout.IntField(new GUIContent("ID", "Reward item id (Optional)."), info.reward.items[j].id);
							if (id != info.reward.items[j].id) {
								Undo.RegisterCompleteObjectUndo(_am, "Edit Reward ID");
								groupInfo.achievementInfos[i].reward.items[j].SetId(id);
							}

							EditorGUI.indentLevel--;

						}

						EditorGUI.indentLevel--;

					}

					EditorGUI.indentLevel--;

				}
			}

			EditorGUI.indentLevel--;

			EditorGUILayout.HelpBox(string.Format("AchievementManager.Report(Achievement.{0}, {1})", _curAchievement, groupInfo.isIncrement ? "increment" : "total"), MessageType.Info);

			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			// Debug Buttons
			GUILayout.BeginHorizontal();
			{
				if (GUILayout.Button("Print Progress"))
					Debug.Log(groupInfo.ToProgressString());
				if (GUILayout.Button("Complete Next Achievement"))
					groupInfo.CompleteNext();
				if (GUILayout.Button("Reward Next Achievement")) {
					if (Application.isPlaying)
						groupInfo.RewardNext();
					else
						Debug.Log("Please run while game is playing.");
				}
				if (GUILayout.Button("Reset Progress"))
					groupInfo.ResetProgress();
			}
			GUILayout.EndHorizontal();

			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			// Delete Button
			GUI.backgroundColor = Color.red;
			if (GUILayout.Button("Delete"))
				_confirmDelete = true;
			GUI.backgroundColor = Color.white;
		}

		private bool AchievementIdExists(string idStr)
		{
			for (int i = 0, imax = EnumUtils.GetCount<Achievement>(); i < imax; i++) {
				if (((Achievement)i).ToString() == idStr)
					return true;
			}
			return false;
		}

		private void AddAchievementId(string idStr)
		{
			if (AchievementIdExists(idStr)) {
				Debug.LogErrorFormat("AchievementManagerEditor:AddAchievementId - AchievementId '{0}' already exists.", idStr);
				return;
			}

			List<string> idStrs = new List<string>();
			for (int i = 0, imax = EnumUtils.GetCount<Achievement>(); i < imax; i++)
				idStrs.Add(((Achievement)i).ToString());
			idStrs.Add(idStr);
			WriteAchievementIds(idStrs);
			_updatedAchievement = string.Empty;
		}

		private void DeleteAchievementId(string idStr)
		{
			List<string> idStrs = new List<string>();
			for (int i = 0, imax = EnumUtils.GetCount<Achievement>(); i < imax; i++) {
				if (((Achievement)i).ToString() == idStr)
					continue;
				idStrs.Add(((Achievement)i).ToString());
			}
			WriteAchievementIds(idStrs);

			if (_curIndex == EnumUtils.GetCount<Achievement>() - 1)
				_curIndex--;
			_updatedAchievement = string.Empty;
		}

		private void ReplaceAchievementId(string prevIdStr, string newIdStr)
		{
			List<string> idStrs = new List<string>();
			for (int i = 0, imax = EnumUtils.GetCount<Achievement>(); i < imax; i++) {
				if (((Achievement)i).ToString() == prevIdStr)
					idStrs.Add(newIdStr);
				else
					idStrs.Add(((Achievement)i).ToString());
			}
			WriteAchievementIds(idStrs);
		}

		private void WriteAchievementIds(List<string> idStrs)
		{
			EditorUtils.WriteScript(modulePath + FILENAME_ACHIEVEMENT, GenerateAchievementIdCode(idStrs), OnWriteAchievementIdsComplete);
			ValidateAchievementInfos(idStrs.Count - 1);
		}

		private void OnWriteAchievementIdsComplete()
		{
			_isCompiling = true;
		}

		private string GenerateAchievementIdCode(List<string> idStrs)
		{
			string code = "public enum Achievement\n";
			code += "{\n";
			foreach (string enumStr in idStrs)
				code += "\t" + enumStr + ",\n";
			code += "}";
			return code;
		}

		private void ValidateAchievementInfos(int count = -1)
		{
			if (count < 0)
				count = EnumUtils.GetCount<Achievement>();
			_am.achievementGroupInfos = _am.achievementGroupInfos.Resize<AchievementGroupInfo>(count);
			_curIndex = Mathf.Min(_curIndex, count - 1);

			// Reassign the id
			for (int i = 0, imax = _am.achievementGroupInfos.Length; i < imax; i++) {
				if (_am.achievementGroupInfos[i].id != (Achievement)i)
					_am.achievementGroupInfos[i].id = (Achievement)i;
			}
		}

		private void OnManagerGUI()
		{
			bool debugMode = EditorGUILayout.Toggle("Debug Mode", _am.debugMode);
			if (debugMode != _am.debugMode) {
				Undo.RegisterCompleteObjectUndo(_am, "Edit Debug Mode");
				_am.debugMode = debugMode;
			}
		}

		[UnityEditor.Callbacks.DidReloadScripts]
		private static void OnScriptsReloaded()
		{
			_isCompiling = false;
		}

	}

}