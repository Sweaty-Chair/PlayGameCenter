using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using SweatyChair;

[CustomEditor(typeof(AchievementManager))]
public class AchievementManagerEditor : Editor
{

	private const string FILENAME_ACHIEVEMENT = "Achievement.cs";
	private const string DIR_MODULE_PATH = "/SweatyChair/Achievements/";

	private string modulePath { get { return Application.dataPath + DIR_MODULE_PATH; } }

	private static bool _isCompiling = false;

	private bool _confirmDelete = false;
	private bool[] _showSubAchievements;

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
			string newIdStr = lastIdIndex >= 0 ? ((Achievement)lastIdIndex).ToString() + "Clone" : "New Achievement";
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

		// ID - use SetNextControlName to avoid it keep update and write to script everytime a key is pressed
		GUI.SetNextControlName("ID");

		if (string.IsNullOrEmpty(_updatedAchievement) && !string.IsNullOrEmpty(_curAchievement.ToString()))
			_updatedAchievement = _curAchievement.ToString();

		_updatedAchievement = EditorGUILayout.TextField("ID", _updatedAchievement);

		if (GUI.GetNameOfFocusedControl() == "ID" && Event.current.isKey && Event.current.keyCode == KeyCode.Return) {
			if (_updatedAchievement != _curAchievement.ToString())
				ReplaceAchievementId(_curAchievement.ToString(), _updatedAchievement);
		}

		if (_updatedAchievement != _curAchievement.ToString())
			EditorGUILayout.HelpBox("Press Enter to Apply", MessageType.Warning);

		// Check the length of achievements is valid
		if (_am.achievementInfos == null || _am.achievementInfos.Length <= _curIndex)
			ValidateAchievementInfos();
		AchievementInfo ai = _am.achievementInfos[_curIndex];

		// Name
		string name = EditorGUILayout.TextField("Name", ai.name);
		if (name != ai.name) {
			Undo.RegisterUndo(_am, "Edit Name");
			ai.name = name;
		}

		// Is incremental
		bool isIncrement = EditorGUILayout.Toggle("Incremental", ai.isIncrement);
		if (isIncrement != ai.isIncrement) {
			Undo.RegisterUndo(_am, "Edit Incremental");
			ai.isIncrement = isIncrement;
		}

		// Has in-game rewards
		bool hasInGameRewards = EditorGUILayout.Toggle("In-Game", ai.isInGame);
		if (hasInGameRewards != ai.isInGame) {
			Undo.RegisterUndo(_am, "Edit In-Game");
			ai.isInGame = hasInGameRewards;
			if (!hasInGameRewards) // Clear the rewards
				ai.rewards = null;
			else
				ai.rewards = ai.rewards.Resize<Reward>(ai.requirements.Length);
		}

		if (ai.isInGame) {
			string description = EditorGUILayout.TextField("Description", ai.description);
			if (description != ai.description) {
				Undo.RegisterUndo(_am, "Edit Description");
				ai.description = description;
			}
		}

		// Total of sub-achivements
		int subAchievementsCount = Mathf.Max(1, EditorGUILayout.IntField("Total Achievements", ai.requirements.Length)); // At least 1
		if (subAchievementsCount != ai.requirements.Length) {
			Undo.RegisterUndo(_am, "Edit Total Achievements");
			Array.Resize<int>(ref ai.requirements, subAchievementsCount);
			Array.Resize<string>(ref ai.iOSIds, subAchievementsCount);
			Array.Resize<string>(ref ai.androidIds, subAchievementsCount);
			if (hasInGameRewards)
				ai.rewards = ai.rewards.Resize<Reward>(subAchievementsCount);
		}

		if (_showSubAchievements == null || _showSubAchievements.Length != subAchievementsCount)
			_showSubAchievements = _showSubAchievements.Resize<bool>(subAchievementsCount, true);

		EditorGUI.indentLevel++;

		// Achievements
		for (int i = 0; i < ai.requirements.Length; i++) {

			_showSubAchievements[i] = EditorGUILayout.Foldout(_showSubAchievements[i], string.Format("Achievement {0}", i + 1));

			if (_showSubAchievements[i]) {
				
				EditorGUI.indentLevel++;

				// Requirement
				int requirement = Mathf.Max(0, EditorGUILayout.IntField("Requirement", ai.requirements[i])); // At least 0
				if (requirement != ai.requirements[i]) {
					Undo.RegisterUndo(_am, "Edit Achievement Requirement");
					ai.requirements[i] = requirement;
				}

				// iOS ID
				string iOSId = EditorGUILayout.TextField("iOS ID", ai.iOSIds[i]);
				if (iOSId != ai.iOSIds[i]) {
					Undo.RegisterUndo(_am, "Edit Achievement iOS ID");
					ai.iOSIds[i] = iOSId;
				}

				// Android ID
				string androidId = EditorGUILayout.TextField("Android ID", ai.androidIds[i]);
				if (androidId != ai.androidIds[i]) {
					Undo.RegisterUndo(_am, "Edit Achievement Android ID");
					ai.androidIds[i] = androidId;
				}

				if (ai.isInGame) {

					// Total rewards
					int rewardCount = EditorGUILayout.IntField("Total Reward Items", ai.rewards[i].count);
					if (rewardCount != ai.rewards[i].count) {
						Undo.RegisterUndo(_am, "Edit Total Reward Items");
						ai.rewards[i].count = rewardCount;
					}

					EditorGUI.indentLevel++;

					// Rewards
					for (int j = 0; j < ai.rewards[i].count; j++) {

						EditorGUILayout.LabelField(string.Format("Reward {0}", j + 1));

						EditorGUI.indentLevel++;

						// Rewards types
						ItemType itemType = (ItemType)EditorGUILayout.EnumPopup("Types", ai.rewards[i].items[j].itemType);
						if (itemType != ai.rewards[i].items[j].itemType) {
							Undo.RegisterUndo(_am, "Edit Reward Types");
							ai.rewards[i].items[j].SetItemType(itemType);
						}

						// Rewards amount
						int amount = EditorGUILayout.IntField("Amount", ai.rewards[i].items[j].amount);
						if (amount != ai.rewards[i].items[j].amount) {
							Undo.RegisterUndo(_am, "Edit Reward Amount");
							ai.rewards[i].items[j].SetAmount(amount);
						}

						EditorGUI.indentLevel--;

					}

					EditorGUI.indentLevel--;

				}

				EditorGUI.indentLevel--;

			}
		}

		EditorGUI.indentLevel--;

		EditorGUILayout.HelpBox(string.Format("AchievementManager.Report(Achievement.{0}, {1})", _curAchievement, ai.isIncrement ? "increment" : "total"), MessageType.Info);

		EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

		// Delete Button
		GUI.backgroundColor = Color.red;
		if (GUILayout.Button("Delete"))
			_confirmDelete = true;
		GUI.backgroundColor = Color.white;
	}

	private void AddAchievementId(string idStr)
	{
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
		_am.achievementInfos = _am.achievementInfos.Resize<AchievementInfo>(count);
		_curIndex = Mathf.Min(_curIndex, count - 1);

		// Reassign the id
		for (int i = 0, imax = _am.achievementInfos.Length; i < imax; i++) {
			if (_am.achievementInfos[i].id != (Achievement)i)
				_am.achievementInfos[i].id = (Achievement)i;
		}
	}

	private void OnManagerGUI()
	{
		bool debugMode = EditorGUILayout.Toggle("Debug Mode", _am.debugMode);
		if (debugMode != _am.debugMode) {
			Undo.RegisterUndo(_am, "Edit Debug Mode");
			_am.debugMode = debugMode;
		}
	}

	[UnityEditor.Callbacks.DidReloadScripts]
	private static void OnScriptsReloaded()
	{
		_isCompiling = false;
	}

}