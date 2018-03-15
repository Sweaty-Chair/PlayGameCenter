using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using SweatyChair;

[CustomEditor(typeof(LeaderboardManager))]
public class LeaderboardManagerEditor : Editor
{

	private const string FILENAME_LEADERBOARD = "Leaderboard.cs";
	private const string DIR_MODULE_PATH = "/SweatyChair/Leaderboards/";

	private string modulePath { get { return Application.dataPath + DIR_MODULE_PATH; } }

	private static bool _isCompiling = false;

	private bool _confirmDelete = false;

	private LeaderboardManager _lm {
		get { return target as LeaderboardManager; }
	}

	private int _cachedCurIndex = -1;

	private int _curIndex {
		get {
			if (_cachedCurIndex == -1)
				_cachedCurIndex = EditorPrefs.GetInt("LeaderboardManagerEditorLeaderboardIndex");
			_cachedCurIndex = Mathf.Max(0, _cachedCurIndex); // Make sure it's positive
			return _cachedCurIndex;
		}
		set {
			_cachedCurIndex = value;
			EditorPrefs.SetInt("LeaderboardManagerEditorLeaderboardIndex", value);
		}
	}

	private Leaderboard _curLeaderboard {
		get { return (Leaderboard)_curIndex; }
	}

	private string _updatedLeaderboard = "";

	public override void OnInspectorGUI()
	{
		if (_isCompiling) {
		
			GUILayout.Label("Wait for compiling finish...");

		} else if (_confirmDelete) {
			
			// Show the confirmation dialog
			GUILayout.Label(string.Format("Sure to delete leaderboard '{0}'?", _curLeaderboard));
			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			GUILayout.BeginHorizontal();
			{
				GUI.backgroundColor = Color.green;

				if (GUILayout.Button("Cancel"))
					_confirmDelete = false;
				
				GUI.backgroundColor = Color.red;

				if (GUILayout.Button("Delete")) {
					DeleteLeaderboardId(_curLeaderboard.ToString());
					_confirmDelete = false;
				}

				GUI.backgroundColor = Color.white;
			}
			GUILayout.EndHorizontal();

		} else {

			OnLeaderboardGUI();

			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			OnManagerGUI();
		}
	}

	private void OnLeaderboardGUI()
	{
		// New
		GUI.backgroundColor = Color.green;
		if (GUILayout.Button("New Leaderboard")) {

			int lastIdIndex = (int)EnumUtils.GetCount<Leaderboard>() - 1;
			string newIdStr = lastIdIndex >= 0 ? ((Leaderboard)lastIdIndex).ToString() + "Clone" : "New Leaderboard";
			AddLeaderboardId(newIdStr);

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
					_updatedLeaderboard = "";
					_curIndex--;
				}
			}
			GUI.color = Color.white;

			_curIndex = EditorGUILayout.IntField(_curIndex + 1, GUILayout.Width(40)) - 1;
			GUILayout.Label("/ " + (EnumUtils.GetCount<Leaderboard>()), GUILayout.Width(40));

			bool canGoNext = true;
			if (_curIndex >= (EnumUtils.GetCount<Leaderboard>() - 1)) {
				GUI.color = Color.grey;
				canGoNext = false;
			}
			if (GUILayout.Button(">>")) {
				if (canGoNext) {
					_confirmDelete = false;
					_updatedLeaderboard = "";
					_curIndex++;
				}
			}
			GUI.color = Color.white;
		}
		GUILayout.EndHorizontal();

		EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

		// ID - use SetNextControlName to avoid it keep update and write to script everytime a key is pressed
		GUI.SetNextControlName("ID");

		if (string.IsNullOrEmpty(_updatedLeaderboard) && !string.IsNullOrEmpty(_curLeaderboard.ToString()))
			_updatedLeaderboard = _curLeaderboard.ToString();

		_updatedLeaderboard = EditorGUILayout.TextField("ID", _updatedLeaderboard);

		if (GUI.GetNameOfFocusedControl() == "ID" && Event.current.isKey && Event.current.keyCode == KeyCode.Return) {
			if (_updatedLeaderboard != _curLeaderboard.ToString())
				ReplaceLeaderboardId(_curLeaderboard.ToString(), _updatedLeaderboard);
		}

		if (_updatedLeaderboard != _curLeaderboard.ToString())
			EditorGUILayout.HelpBox("Press Enter to Apply", MessageType.Warning);

		if (_lm.leaderboardInfos == null || _lm.leaderboardInfos.Length <= _curIndex)
			ValidateLeaderboardInfos();

		LeaderboardInfo li = _lm.leaderboardInfos[_curIndex];

		// iOS ID
		string iOSId = EditorGUILayout.TextField("iOS ID", li.iOSId);
		if (iOSId != li.iOSId) {
			Undo.RegisterUndo(_lm, "Reassign Leaderboard iOS ID");
			li.iOSId = iOSId;
		}

		// Android ID
		string androidId = EditorGUILayout.TextField("Android ID", li.androidId);
		if (androidId != li.androidId) {
			Undo.RegisterUndo(_lm, "Reassign Leaderboard Android ID");
			li.androidId = androidId;
		}
		// Is incremental
		bool isIncrement = EditorGUILayout.Toggle("Incremental", li.isIncrement);
		if (isIncrement != li.isIncrement) {
			Undo.RegisterUndo(_lm, "Edit Incremental");
			li.isIncrement = isIncrement;
		}
		
		EditorGUILayout.HelpBox(string.Format("LeaderboardManager.Report(Leaderboard.{0}, {1})", _curLeaderboard, li.isIncrement ? "incrementScore" : "score"), MessageType.Info);

		EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

		// Delete Button
		GUI.backgroundColor = Color.red;
		if (GUILayout.Button("Delete"))
			_confirmDelete = true;
		GUI.backgroundColor = Color.white;
	}

	private void AddLeaderboardId(string idStr)
	{
		List<string> idStrs = new List<string>();
		for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++)
			idStrs.Add(((Leaderboard)i).ToString());
		idStrs.Add(idStr);
		WriteLeaderboardIds(idStrs);
		_updatedLeaderboard = string.Empty;
	}

	private void DeleteLeaderboardId(string idStr)
	{
		List<string> idStrs = new List<string>();
		for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
			if (((Leaderboard)i).ToString() == idStr)
				continue;
			idStrs.Add(((Leaderboard)i).ToString());
		}
		WriteLeaderboardIds(idStrs);

		if (_curIndex == EnumUtils.GetCount<Leaderboard>() - 1)
			_curIndex--;
		_updatedLeaderboard = string.Empty;
	}

	private void ReplaceLeaderboardId(string prevIdStr, string newIdStr)
	{
		List<string> idStrs = new List<string>();
		for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
			if (((Leaderboard)i).ToString() == prevIdStr)
				idStrs.Add(newIdStr);
			else
				idStrs.Add(((Leaderboard)i).ToString());
		}
		WriteLeaderboardIds(idStrs);
	}

	private void WriteLeaderboardIds(List<string> idStrs)
	{
		EditorUtils.WriteScript(modulePath + FILENAME_LEADERBOARD, GenerateLeaderboardIdCode(idStrs), OnWriteLeaderboardIdsComplete);
		ValidateLeaderboardInfos(idStrs.Count - 1);
	}

	private void OnWriteLeaderboardIdsComplete()
	{
		_isCompiling = true;
	}

	private string GenerateLeaderboardIdCode(List<string> idStrs)
	{
		string code = "public enum Leaderboard\n";
		code += "{\n";
		foreach (string leaderboardName in idStrs)
			code += "\t" + leaderboardName + ",\n";
		code += "}";
		return code;
	}

	private void ValidateLeaderboardInfos(int count = -1)
	{
		if (count < 0)
			count = EnumUtils.GetCount<Leaderboard>();
		_lm.leaderboardInfos = _lm.leaderboardInfos.Resize<LeaderboardInfo>(count);
		_curIndex = Mathf.Min(_curIndex, count - 1);

		// Reassign the id
		for (int i = 0, imax = _lm.leaderboardInfos.Length; i < imax; i++) {
			if (_lm.leaderboardInfos[i].id != (Leaderboard)i)
				_lm.leaderboardInfos[i].id = (Leaderboard)i;
		}
	}

	private void OnManagerGUI()
	{
		bool hasInGameLeaderboards = EditorGUILayout.Toggle("Has In Game Leaderboards", _lm.hasInGameLeaderboards);
		if (hasInGameLeaderboards != _lm.hasInGameLeaderboards) {
			Undo.RegisterUndo(_lm, "Reassign Has In Game Leaderboards");
			_lm.hasInGameLeaderboards = hasInGameLeaderboards;
		}

		if (_lm.hasInGameLeaderboards) {
			bool loadTopScores = EditorGUILayout.Toggle("Load Top Scores", _lm.loadTopScores);
			if (loadTopScores != _lm.loadTopScores) {
				Undo.RegisterUndo(_lm, "Reassign Load Top Scores");
				_lm.loadTopScores = loadTopScores;
			}
		}

		if (_lm.hasInGameLeaderboards && _lm.loadTopScores) {
			int numTopScores = EditorGUILayout.IntField("Number Top Scores", _lm.numTopScores);
			if (numTopScores != _lm.numTopScores) {
				Undo.RegisterUndo(_lm, "Reassign Number Top Scores");
				_lm.numTopScores = numTopScores;
			}
		}

		bool loadMyScores = EditorGUILayout.Toggle("Load My Scores", _lm.loadMyScores);
		if (loadMyScores != _lm.loadMyScores) {
			Undo.RegisterUndo(_lm, "Reassign Load Top Scores");
			_lm.loadMyScores = loadMyScores;
		}

		bool debugMode = EditorGUILayout.Toggle("Debug Mode", _lm.debugMode);
		if (debugMode != _lm.debugMode) {
			Undo.RegisterUndo(_lm, "Reassign Debug Mode");
			_lm.debugMode = debugMode;
		}
	}

	[UnityEditor.Callbacks.DidReloadScripts]
	private static void OnScriptsReloaded()
	{
		_isCompiling = false;
	}
		
}