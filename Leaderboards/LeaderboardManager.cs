using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms;
using System;
using System.Collections;
using System.Collections.Generic;
using Prime31;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SweatyChair
{

	public class LeaderboardManager : Singleton<LeaderboardManager>
	{

		#if UNITY_ANDROID
		// + Leaderboard, e.g. PlayGameCenterCachedHighScoreDistance, PlayGameCenterCachedHighScoreHeight
		private const string PREF_CACHED_HIGH_SCORE = "LeaderboardManagerCachedHighScore";
		#endif

		// PlayerPrefs Keys
		private const string PREF_CURRENT_TIME_SCOPE = "CurrentTimeScope";
		// +Leaderboard, e.g. PlayerScoreWinMatch, PlayerScoreSecondLeaderboard
		private const string PREF_MY_LEADERBOARD_SCORE = "PlayerScore";
		// +Leaderboard+TimeScore+index, e.g. TopScoresWinMatchAllTime0, TopScoresSecondLeaderboardWeek1
		private const string PREF_TOP_SCORES = "TopScores";

		// Events
		public static event UnityAction<bool> uiToggledEvent;
		public static event UnityAction<Leaderboard, long> myScoreLoadedEvent;
		public static event UnityAction<Leaderboard, TimeScope, List<LeaderboardEntry>> topScoresLoadedEvent;
		public static event UnityAction<Leaderboard, long> highscoreObtainedEvent;
		public static event UnityAction<Leaderboard> topScoresChangedEvent;

		public static TimeScope currentTimeScope = TimeScope.AllTime;

		public LeaderboardInfo[] leaderboardInfos;

		// Toogle to determine having in-game scores
		public bool hasInGameLeaderboards = false;
		// Toogle to load top scores and fire the result with topScoresLoadedEvent
		public bool shouldLoadTopScores = false;
		// Number top scores to download
		public int loadTopScoreCount = 10;
		// Toggle to load my scores and fire the result with myScoreLoadedEvent
		public bool shouldLoadMyScores = false;

		public bool debugMode = false;
	
		// Current loaded leaderboard-timescope pair, timescopes are loaded on sequence for each leaderboard, e.g. DefaultLeaderboard-AllTime, then DefaultLeaderboard-Week, then DefaultLeaderboard-Daily
		private static Dictionary<Leaderboard, TimeScope> _topScoresLoadedTimeScopeDict = new Dictionary<Leaderboard, TimeScope>();
		// Current loaded leaderboard-boolean pair, leaderboards are loaded on sequence for my scores, e.g. DefaultLeaderboard, SecondLeaderboard, etc.
		private static Dictionary<Leaderboard, bool> _isMyScoreLoadedDict = new Dictionary<Leaderboard, bool>();
	
		// The all-time leaderboard score entry, for different leaderboard names
		private static LeaderboardEntry[] _myLeaderboardEntries = new LeaderboardEntry[0];
	
		// LeaderboardEntry lists by leaderboard
		private static Dictionary<Leaderboard, List<LeaderboardEntry>> _topScoresAllTimeDict = new Dictionary<Leaderboard, List<LeaderboardEntry>>();
		private static Dictionary<Leaderboard, List<LeaderboardEntry>> _topScoresWeekDict = new Dictionary<Leaderboard, List<LeaderboardEntry>>();
		private static Dictionary<Leaderboard, List<LeaderboardEntry>> _topScoresTodayDict = new Dictionary<Leaderboard, List<LeaderboardEntry>>();

		#region Change this for different game

		private static Leaderboard currentLeaderboard {
			get {
				// TODO
				return (Leaderboard)0;
			}
		}

		#endregion

		public static string myName {
			get {
				if (GetMyLeaderboardEntry((Leaderboard)0) == null)
					return "You";
				return GetMyLeaderboardEntry((Leaderboard)0).name;
			}
		}

		public static long myScore {
			get { return GetMyLeaderboardScore(currentLeaderboard); }
		}

		public static float myFloatScore {
			get { return GetMyLeaderboardFloatScore(currentLeaderboard); }
		}

		public static float highestDecimalScore { // Highest decimal score in all leader boards, used only for checking Android offline survival seconds achievements
			get {
				float tmp = 0;
				for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
					float timeScore = GetMyLeaderboardFloatScore((Leaderboard)i);
					tmp = Mathf.Max(tmp, timeScore);
				}
				return tmp;
			}
		}

		public static int numLoadedTopScores {
			get {
				if (!instanceExists || !instance.shouldLoadTopScores)
					return 0;
				return instance.loadTopScoreCount;
			}
		}

		protected override void Awake()
		{
			base.Awake();

			// Return and not initialize score variables, if no in-game leaderboard needed
			if (!hasInGameLeaderboards)
				return;

			// Init all arrays, lists and dictionaries here
			_myLeaderboardEntries = new LeaderboardEntry [EnumUtils.GetCount<Leaderboard>()];
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				_topScoresAllTimeDict.Add((Leaderboard)i, new List<LeaderboardEntry>());
				_topScoresWeekDict.Add((Leaderboard)i, new List<LeaderboardEntry>());
				_topScoresTodayDict.Add((Leaderboard)i, new List<LeaderboardEntry>());
			}

			// Read PlayerPref
			ReadMyLeaderboardScores();
			ReadTopScores();

			currentTimeScope = (TimeScope)PlayerPrefs.GetInt(PREF_CURRENT_TIME_SCOPE);

			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				if (shouldLoadMyScores)
					_isMyScoreLoadedDict.Add((Leaderboard)i, false);
				if (shouldLoadTopScores)
					_topScoresLoadedTimeScopeDict.Add((Leaderboard)i, TimeScope.AllTime);
			}

			#if UNITY_IOS || UNITY_TVOS
			GameCenterManager.scoresLoadedEvent += OnTopScoresLoaded;
			GameCenterManager.scoresForPlayerIdsLoadedEvent += OnMyScoreLoaded;
			#elif UNITY_ANDROID && !CHS
			GPGManager.loadScoresSucceededEvent += OnTopScoresLoaded;
			GPGManager.loadCurrentPlayerLeaderboardScoreSucceededEvent += OnMyScoreLoaded;
			#endif

			PlayGameCenterManager.authenticationSucceededEvent += CheckshouldLoadTopScores;
		}

		#region Leaderboard top scores loaded callbacks

		private void CheckshouldLoadTopScores()
		{
			if (shouldLoadMyScores) {
				foreach (LeaderboardInfo li in leaderboardInfos) {
					#if UNITY_IOS || UNITY_TVOS
					GameCenterBinding.retrieveScoresForPlayerIds(new string[1]{ GameCenterBinding.playerIdentifier() }, li.leaderboardId);
					#elif UNITY_ANDROID && !CHS
					PlayGameServices.loadCurrentPlayerLeaderboardScore(li.leaderboardId, GPGLeaderboardTimeScope.AllTime, false);
					#endif
				}
			}

			#if UNITY_ANDROID
			CheckOfflineHighScores();
			#endif

			if (shouldLoadTopScores)
				DownloadAllLeaderboardTopScores(TimeScope.AllTime);
		}

		#if UNITY_IOS || UNITY_TVOS

		private void OnTopScoresLoaded(GameCenterRetrieveScoresResult retrieveScoresResult)
		{
			if (debugMode) {
				Debug.LogFormat("LeaderboardManager:OnTopScoresLoaded({0}) - _curRetrivingTimeScope: {1}", retrieveScoresResult.scores.Count, _topScoresLoadedTimeScopeDict);
				DebugUtils.Log(retrieveScoresResult);
			}

			if (retrieveScoresResult == null || retrieveScoresResult.scores == null || retrieveScoresResult.scores.Count == 0)
				return;

			ProcessTopScores(GetLeaderboard(retrieveScoresResult.scores[0]), GetLeaderboardEntryList(retrieveScoresResult.scores));
		}

		#elif UNITY_ANDROID && !CHS
		
		private void OnTopScoresLoaded(List<GPGScore> scores)
		{
			if (debugMode) {
				Debug.LogFormat("LeaderboardManager:OnTopScoresLoaded({0}) - _curRetrivingTimeScope: {1}", scores.Count);
				DebugUtils.Log(scores);
			}

			if (scores == null || scores.Count == 0)
			return;

			ProcessTopScores(GetLeaderboard(scores[0]), GetLeaderboardEntryList(scores));
		}

		#endif

		private void ProcessTopScores(Leaderboard leaderboard, List<LeaderboardEntry> scores)
		{
			if (_topScoresLoadedTimeScopeDict[leaderboard] == TimeScope.AllTime) {

				_topScoresLoadedTimeScopeDict[leaderboard] = TimeScope.Week;
				DownloadAllLeaderboardTopScores(TimeScope.Week);

			} else if (_topScoresLoadedTimeScopeDict[leaderboard] == TimeScope.Week) {

				_topScoresLoadedTimeScopeDict[leaderboard] = TimeScope.Today;
				DownloadAllLeaderboardTopScores(TimeScope.Today);

			}

			SetTopScores(leaderboard, _topScoresLoadedTimeScopeDict[leaderboard], scores);

			if (topScoresLoadedEvent != null)
				topScoresLoadedEvent(leaderboard, _topScoresLoadedTimeScopeDict[leaderboard], scores);
		}

		#endregion

		#region Leaderboard my score loaded callbacks

		#if UNITY_IOS || UNITY_TVOS

		private void OnMyScoreLoaded(GameCenterRetrieveScoresResult retrieveScoresResult)
		{
			if (retrieveScoresResult.scores.Count == 0 || retrieveScoresResult.scores[0] == null)
				return; // Just in case

			if (debugMode)
				Debug.Log("LeaderboardManager:OnMyScoreLoaded()");

			foreach (GameCenterScore gcs in retrieveScoresResult.scores) {
				Leaderboard l = GetLeaderboard(gcs);

				if (_isMyScoreLoadedDict[l]) // Just get the 'all-time' score for once
					continue;
				_isMyScoreLoadedDict[l] = true;

				if (debugMode)
					DebugUtils.Log(gcs);

				ProcessMyLoadedScore(l, gcs.value);
			}
		}

		#elif UNITY_ANDROID && !CHS
		
		private void OnMyScoreLoaded(GPGScore score)
		{
			Leaderboard l = GetLeaderboard(score);

			if (_isMyScoreLoadedDict[l]) // Just get the 'all-time' score for once
				return;
			_isMyScoreLoadedDict[l] = true;

			if (debugMode) {
				Debug.Log("LeaderboardManager:OnMyScoreLoaded()");
				DebugUtils.Log(score);
			}

			ProcessMyLoadedScore(l, score.value);
		}

		#endif

		private void ProcessMyLoadedScore(Leaderboard leaderboard, long score)
		{
			// Replace local score entry with the online one ONLY when online score is higher
			if (GetMyLeaderboardScore(leaderboard) < score)
				SetMyLeaderboardScore(leaderboard, score);
			if (myScoreLoadedEvent != null)
				myScoreLoadedEvent(leaderboard, score);
		}

		#endregion

		#region Show Game Center / Play Games UI

		public static void ShowPlayGameCenterLeaderboard(int leaderboardIndex)
		{
			ShowPlayGameCenterLeaderboard((Leaderboard)leaderboardIndex);
		}

		public static void ShowPlayGameCenterLeaderboard(Leaderboard leaderboard, TimeScope timeScope = TimeScope.AllTime)
		{
			if (!instanceExists)
				return;
			if (!PlayGameCenterManager.isAuthenticated)
				PlayGameCenterManager.TryAuthentication(true);
			#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.showLeaderboardWithTimeScopeAndLeaderboard(TimeScope2GameCenterLeaderboardTimeScope(timeScope), s_Instance.leaderboardInfos[(int)leaderboard].leaderboardId);
			#elif UNITY_ANDROID && !CHS
			PlayGameServices.showLeaderboard(s_Instance.leaderboardInfos[(int)leaderboard].leaderboardId);
			#endif
		}

		public static void ShowPlayGameCenterLeaderboards()
		{
			if (!PlayGameCenterManager.isAuthenticated)
				PlayGameCenterManager.TryAuthentication(true);
			#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.showLeaderboardWithTimeScope(GameCenterLeaderboardTimeScope.AllTime);
			#elif UNITY_ANDROID && !CHS
			PlayGameServices.showLeaderboards();
			#endif
		}

		#endregion

		#region Leaderboards

		public static void DownloadAllLeaderboardTopScores(TimeScope timeScope)
		{
			if (instanceExists && instance.debugMode)
				Debug.LogFormat("LeaderboardManager:DownloadAllLeaderboardTopScores({0})", timeScope);

			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++)
				DownloadLeaderboardTopScores((Leaderboard)i, timeScope);
		}

		public static void DownloadLeaderboardTopScores(Leaderboard leaderboard, TimeScope timeScope = TimeScope.AllTime)
		{
			if (!instanceExists)
				return;
			
			if (instance.debugMode)
				Debug.LogFormat("LeaderboardManager:DownloadLeaderboardTopScores({0},{1})", leaderboard, timeScope);

			#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.retrieveScores(false, TimeScope2GameCenterLeaderboardTimeScope(timeScope), 1, s_Instance.loadTopScoreCount, s_Instance.leaderboardInfos[(int)leaderboard].leaderboardId);
			#elif UNITY_ANDROID && !CHS
			PlayGameServices.loadScoresForLeaderboard(s_Instance.leaderboardInfos[(int)leaderboard].leaderboardId, TimeScope2GPGLeaderboardTimeScope(timeScope), false, false);
			#endif
		}

		public static void Report(int score)
		{
			Report((long)score);
		}

		public static void Report(long score)
		{
			if (!instanceExists)
				return;

			if (instance.debugMode)
				Debug.LogFormat("ScoreManager:ReportScore({0})", score);

			Report(currentLeaderboard, score); // Submit the score to Game Center / Play Games anyway, highscore or not is handle in their end

			long myLocalScore = GetMyLeaderboardScore(currentLeaderboard);

			if (instance.debugMode)
				Debug.LogFormat("ScoreManager:ReportScore - score={0}, myLocalScore={1}", score, myLocalScore);

			if (myLocalScore >= score) // No local highscore changed, return
				return;

			OnHighscoreObtain(score);

			SetMyLeaderboardScore(currentLeaderboard, score);

			if (instance.shouldLoadTopScores) // Skip if no top scores to compair
				return;

			// Only try update leaderboards when getting a local highscore, to save network brandwidth
			CompareScoreToLeaderboard(currentLeaderboard);
			if (PlayGameCenterManager.isAuthenticated) {
				CompareScoreToLeaderboard(currentLeaderboard, TimeScope.Week);
				CompareScoreToLeaderboard(currentLeaderboard, TimeScope.Today);
			}
		}

		public static void Report(Leaderboard leaderboard, int score)
		{
			Report(leaderboard, (long)score);
		}

		public static void Report(Leaderboard leaderboard, long score)
		{
			if (!instanceExists)
				return;
			
			int leaderboardIndex = (int)leaderboard;

			if (leaderboardIndex >= instance.leaderboardInfos.Length) {
				Debug.LogFormat("LeaderboardManager:Report - leaderboard index ({0}) out of bound.", leaderboardIndex);
				return;
			}

			if (instance.debugMode)
				Debug.LogFormat("LeaderboardManager:Report({0},{1})", leaderboard, score);

			#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.reportScore(score, s_Instance.leaderboardInfos[leaderboardIndex].leaderboardId);
			#elif UNITY_ANDROID && !CHS
			if (PlayGameCenterManager.isAuthenticated)
				PlayGameServices.submitScore(s_Instance.leaderboardInfos[leaderboardIndex].leaderboardId, score);
			else
				SetCachedHighScore(leaderboard, score);
			#endif
		}

		// Game Center take care of offline score and achievements automatically, Play Games sucks and need to take care of with cache
		#if UNITY_ANDROID
		
		private static long GetCachedHighScore(Leaderboard leaderboard)
		{
			return (long)PlayerPrefs.GetInt(PREF_CACHED_HIGH_SCORE + leaderboard.ToString());
		}

		private static void SetCachedHighScore(Leaderboard leaderboard, long score)
		{
			if (score > GetCachedHighScore(leaderboard)) // Only cache the larger score
				PlayerPrefs.SetFloat(PREF_CACHED_HIGH_SCORE + leaderboard.ToString(), score);
		}

		private static void CheckOfflineHighScores()
		{
			// iOS no need because it caches offline score automatically
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				Leaderboard l = (Leaderboard)i;
				long cachedHighScore = GetCachedHighScore(l);
				if (cachedHighScore > 0) {
					Debug.LogFormat("LeaderboardManager:CheckOfflineHighscores - Report a offline {0} score: {1}", l, cachedHighScore);
					Report(l, cachedHighScore);
					SetCachedHighScore(l, 0); // Unset it
				}
			}
		}

		#endif

		#endregion

		#region Type Convention

		#if UNITY_IOS || UNITY_TVOS

		private static GameCenterLeaderboardTimeScope TimeScope2GameCenterLeaderboardTimeScope(TimeScope timeScope)
		{
			return (GameCenterLeaderboardTimeScope)((int)timeScope);
		}

		private static Leaderboard GetLeaderboard(GameCenterScore gScore)
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				if (gScore.category == s_Instance.leaderboardInfos[i].leaderboardId)
					return (Leaderboard)i;
			}
			return (Leaderboard)0;
		}

		private static List<LeaderboardEntry> GetLeaderboardEntryList(List<GameCenterScore> gScores)
		{
			List<LeaderboardEntry> tmp = new List<LeaderboardEntry>();
			foreach (GameCenterScore s in gScores)
				tmp.Add(new LeaderboardEntry(s));
			return tmp;
		}

		#elif UNITY_ANDROID && !CHS
		
		private static GPGLeaderboardTimeScope TimeScope2GPGLeaderboardTimeScope(TimeScope timeScope)
		{
			return (GPGLeaderboardTimeScope)((int)timeScope + 1); // GPGLeaderboardTimeScope has a Unkown as first, so +1
		}

		private static Leaderboard GetLeaderboard(GPGScore gScore)
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				if (gScore.leaderboardId == s_Instance.leaderboardInfos[i].leaderboardId)
					return (Leaderboard)i;
			}
			return (Leaderboard)0;
		}

		private static List<LeaderboardEntry> GetLeaderboardEntryList(List<GPGScore> gScores)
		{
			List<LeaderboardEntry> tmp = new List<LeaderboardEntry>();
			foreach (GPGScore s in gScores)
				tmp.Add(new LeaderboardEntry(s));
			return tmp;
		}

		#endif

		#endregion

		public static void SetCurrentTimeScope(TimeScope timeScope)
		{
			currentTimeScope = timeScope;
			PlayerPrefs.SetInt(PREF_CURRENT_TIME_SCOPE, (int)timeScope);
		}

		#region Events

		private static void OnTopScoresChange(Leaderboard leaderboard, TimeScope timeScope)
		{
			if (topScoresChangedEvent != null && timeScope == currentTimeScope)
				topScoresChangedEvent(leaderboard);
		}

		private static void OnHighscoreObtain(long score)
		{
			if (highscoreObtainedEvent != null)
				highscoreObtainedEvent(currentLeaderboard, score);
		}

		#endregion

		#region My Leaderboard Scores

		private static void ReadMyLeaderboardScores()
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				Leaderboard l = (Leaderboard)i;
				string rawData = PlayerPrefs.GetString(PREF_MY_LEADERBOARD_SCORE + l, "You|11|0");
				string[] rawDataArray = rawData.Split('|');
				_myLeaderboardEntries[(int)l] = new LeaderboardEntry(rawDataArray);
			}
		}

		private static void SetMyLeaderboardEntry(Leaderboard leaderboard, LeaderboardEntry leaderboardEntry)
		{
			_myLeaderboardEntries[(int)leaderboard] = leaderboardEntry;
			WriteMyLeaderboardEntry(leaderboard);
		}

		private static void SetMyLeaderboardScore(Leaderboard leaderboard, long score)
		{
			LeaderboardEntry le = GetMyLeaderboardEntry(leaderboard);
			le.score = score;
			WriteMyLeaderboardEntry(leaderboard);
		}

		private static void SetMyLeaderboardRank(Leaderboard leaderboard, int rank)
		{
			LeaderboardEntry le = GetMyLeaderboardEntry(leaderboard);
			le.rank = rank;
			WriteMyLeaderboardEntry(leaderboard);
		}

		private static void WriteMyLeaderboardEntry(Leaderboard leaderboard = (Leaderboard)0)
		{
			LeaderboardEntry le = GetMyLeaderboardEntry(leaderboard);
			if (le == null) // Just in case
				return;
			PlayerPrefs.SetString(PREF_MY_LEADERBOARD_SCORE + leaderboard.ToString(), le.prefData);
		}

		private static int GetMyLeaderboardRank(Leaderboard leaderboard = (Leaderboard)0, TimeScope timeScope = TimeScope.AllTime)
		{
			List<LeaderboardEntry> topScores;
			int loadTopScoreCount;
			if (timeScope == TimeScope.AllTime) { // Just my leaderboard score
				return GetMyLeaderboardEntry(leaderboard).rank;
			} else { // Loop and find
				topScores = GetTopScores(leaderboard, timeScope);
				loadTopScoreCount = topScores.Count;
				for (int i = 0, imax = loadTopScoreCount; i < imax; i++) {
					if (topScores[i].name == myName)
						return topScores[i].rank;
				}
				return loadTopScoreCount + 1; // Return the top scores people + 1
			}
		}

		public static long GetMyLeaderboardScore(Leaderboard leaderboard = (Leaderboard)0)
		{
			LeaderboardEntry le = GetMyLeaderboardEntry(leaderboard);
			if (le == null)
				return 0;
			return le.score;
		}

		public static float GetMyLeaderboardFloatScore(Leaderboard leaderboard = (Leaderboard)0)
		{
			LeaderboardEntry le = GetMyLeaderboardEntry(leaderboard);
			if (le == null)
				return 0;
			return le.floatScore;
		}

		public static LeaderboardEntry GetMyLeaderboardEntry(Leaderboard leaderboard = (Leaderboard)0)
		{
			if (_myLeaderboardEntries.Length <= (int)leaderboard)
				return null;
			return _myLeaderboardEntries[(int)leaderboard];
		}

		#endregion

		#region Top Scores

		private void ReadTopScores()
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) { // For each leaderboard

				Leaderboard l = (Leaderboard)i;

				// No score for this leaderboard, preset it
				if (string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_TOP_SCORES + l + TimeScope.AllTime + 0, string.Empty))) {

					ReadPresetTopScores(l);

				} else { // Load from the saved top scores

					for (int j = 0, jmax = LeaderboardManager.numLoadedTopScores; j < jmax; j++) { // For each rank
						AddTopScore(ReadTopScoreRawData(l, TimeScope.AllTime, j), l, TimeScope.AllTime);
						AddTopScore(ReadTopScoreRawData(l, TimeScope.Week, j), l, TimeScope.Week);
						AddTopScore(ReadTopScoreRawData(l, TimeScope.Today, j), l, TimeScope.Today);
					}

				}
			}
		}

		private static string[] ReadTopScoreRawData(Leaderboard leaderboard, TimeScope timeScope, int index)
		{
			return PlayerPrefs.GetString(PREF_TOP_SCORES + leaderboard + timeScope + index).Split('|');
		}

		private static readonly string[] PRESET_SCORES_NAME = new string [] {
			"Isaac Newton",
			"Sweaty Chair",
			"Bill Gates",
			"Mickey Mouse",
			"James Bond",
			"Hello Kitty",
			"Yao Ming",
			"Homer Simpson",
			"Lady Gaga",
			"Justin Bieber"
		};
		private static readonly int[] PRESET_SCORES_SCORES = new int [] {
			3000,
			2600,
			2200,
			2000,
			1500,
			1000,
			700,
			500,
			300,
			100
		};

		private void ReadPresetTopScores(Leaderboard leaderboard)
		{
			if (debugMode)
				Debug.LogFormat("ScoreManager:ReadPresetTopScores({0})", leaderboard);

			ReadPresetTopScores(leaderboard, TimeScope.AllTime);
			ReadPresetTopScores(leaderboard, TimeScope.Week);
			ReadPresetTopScores(leaderboard, TimeScope.Today);
		}

		private void ReadPresetTopScores(Leaderboard leaderboard, TimeScope timeScope)
		{
			switch (timeScope) {
				case TimeScope.AllTime:
					_topScoresAllTimeDict[leaderboard].Clear();
					break;
				case TimeScope.Week:
					_topScoresWeekDict[leaderboard].Clear();
					break;
				case TimeScope.Today:
					_topScoresTodayDict[leaderboard].Clear();
					break;
			}

			string[] rawData = new string [3];
			for (int i = 0, imax = PRESET_SCORES_NAME.Length; i < imax; i++) {
				rawData[0] = PRESET_SCORES_NAME[i];
				rawData[1] = "" + (i + 1);
				rawData[2] = "" + PRESET_SCORES_SCORES[i];
				AddTopScore(rawData, leaderboard, timeScope);
			}

			WriteTopScores(leaderboard, timeScope);
		}

		private void AddTopScore(string[] scoreData, Leaderboard leaderboard = (Leaderboard)0, TimeScope timeScope = TimeScope.AllTime)
		{
			if (!LeaderboardEntry.CheckRawDataValid(scoreData)) { // Data not valid, skip it, this should not be happens excepts no top scores in newly made leaderboards
				Debug.LogWarning("ScoreManager:AddTopScore - scoreData invalid: " + scoreData);
				return;
			}
			switch (timeScope) {
				case TimeScope.AllTime:
					_topScoresAllTimeDict[leaderboard].Add(new LeaderboardEntry(scoreData));
					break;
				case TimeScope.Week:
					_topScoresWeekDict[leaderboard].Add(new LeaderboardEntry(scoreData));
					break;
				default:
				case TimeScope.Today:
					_topScoresTodayDict[leaderboard].Add(new LeaderboardEntry(scoreData));
					break;
			}
		}

		// Call only when successfully downloaded online leaderboard or when there is a change on leaderboard.
		public static void WriteTopScores(Leaderboard leaderboard = (Leaderboard)0, TimeScope timeScope = TimeScope.AllTime)
		{
			List<LeaderboardEntry> topScores = GetTopScores(leaderboard, timeScope);
			int loadTopScoreCount = topScores.Count;

			for (int i = 0, imax = LeaderboardManager.numLoadedTopScores; i < imax; i++) {
				if (i >= loadTopScoreCount) // This only happens in new leaderboard having not enough top scores
				break;
				PlayerPrefs.SetString(PREF_TOP_SCORES + leaderboard + timeScope + i, topScores[i].prefData);
			}

			OnTopScoresChange(leaderboard, timeScope);
		}

		public static List<LeaderboardEntry> GetTopScores(Leaderboard leaderboard = (Leaderboard)0, TimeScope timeScope = TimeScope.AllTime)
		{
			switch (timeScope) {
				case TimeScope.AllTime:
					return _topScoresAllTimeDict[leaderboard];
				case TimeScope.Week:
					return _topScoresWeekDict[leaderboard];
				default:
				case TimeScope.Today:
					return _topScoresTodayDict[leaderboard];
			}
		}

		public static void SetTopScores(Leaderboard leaderboard, TimeScope timeScope, List<LeaderboardEntry> value)
		{
			switch (timeScope) {
				case TimeScope.AllTime:
					_topScoresAllTimeDict[leaderboard] = value;
					break;
				case TimeScope.Week:
					_topScoresWeekDict[leaderboard] = value;
					break;
				default:
				case TimeScope.Today:
					_topScoresTodayDict[leaderboard] = value;
					break;
			}
			WriteTopScores(leaderboard, timeScope);
		}

		public static LeaderboardEntry GetTopScores(Leaderboard leaderboard, TimeScope timeScope, int index)
		{
			List<LeaderboardEntry> topScores = GetTopScores(leaderboard, timeScope);
			if (index >= topScores.Count)
				return null;
			return topScores[index];
		}

		#endregion

		// Compares the player score with the leaderboard, and do update if neccessary, for offline only
		// If online and connected to Game Center / Play Games, this is not neccessary and new downloaded leaderboard will override this
		private static void CompareScoreToLeaderboard(Leaderboard leaderboard = (Leaderboard)0, TimeScope timeScope = TimeScope.AllTime)
		{
			if (!instanceExists)
				return;

			if (instance.debugMode)
				Debug.LogFormat("LeaderboardManager:CompareScoreToLeaderboard({0},{1})", leaderboard, timeScope);

			List<LeaderboardEntry> topScores = GetTopScores(leaderboard, timeScope);
			LeaderboardEntry myLeaderboardEntry = GetMyLeaderboardEntry(leaderboard);

			int loadTopScoreCount = topScores.Count;
			int myRank = GetMyLeaderboardRank(leaderboard, timeScope);

			bool topScoresChanged = myRank <= loadTopScoreCount;
			if (topScoresChanged) // Update my rank row first
			topScores[myRank - 1] = new LeaderboardEntry(GetMyLeaderboardEntry(leaderboard), myRank);

			for (int i = Mathf.Min(loadTopScoreCount, myRank - 1) - 1; i >= 0; i--) { // From either my upper rank, or top scores bottom, check toward top

				LeaderboardEntry le = topScores[i]; // This top score entry

				if (GetMyLeaderboardScore(leaderboard) <= le.score) // Not better than this top score, just break
					break;

				topScoresChanged = true;

				int newRank = i + 1; // i >= 0 but rank >= 1, so +1
				if (timeScope == TimeScope.AllTime)
					SetMyLeaderboardRank(leaderboard, newRank);

				topScores[i] = new LeaderboardEntry(myLeaderboardEntry, newRank);
				Debug.Log(i + "|" + topScores[i]);
				// Move the ranks down
				if (i == loadTopScoreCount - 1) // Skip if the bottom
					continue;
				topScores[i + 1] = new LeaderboardEntry(le, newRank + 1);
			}

			if (instance.debugMode)
				Debug.LogFormat("LeaderboardManager:CompareScoreToLeaderboard - topScoresChanged={0}", topScoresChanged);

			if (topScoresChanged) {
				SetTopScores(leaderboard, timeScope, topScores);
				// Do a update too, the online score may also get changed
				DownloadLeaderboardTopScores(leaderboard, timeScope);
			}
		}

		public static void ToggleUI(bool isShown = true)
		{
			if (uiToggledEvent != null)
				uiToggledEvent(isShown);
		}

		#if UNITY_EDITOR

		[MenuItem("Debug/Leaderboards/Delete All My Scores", false, 600)]
		private static void DeleteMyLeaderboardScore()
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++)
				PlayerPrefs.DeleteKey(PREF_MY_LEADERBOARD_SCORE + ((Leaderboard)i).ToString());
			if (Application.isPlaying)
				ReadMyLeaderboardScores();
		}

		[MenuItem("Debug/Leaderboards/Delete All Top Scores")]
		private static void DeleteAllTopScores()
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) { // For each leaderboard name
				for (int j = 0, jmax = LeaderboardManager.numLoadedTopScores; j < jmax; j++) { // For each rank
					PlayerPrefs.DeleteKey(PREF_TOP_SCORES + ((Leaderboard)i) + TimeScope.AllTime + j);
					PlayerPrefs.DeleteKey(PREF_TOP_SCORES + ((Leaderboard)i) + TimeScope.Week + j);
					PlayerPrefs.DeleteKey(PREF_TOP_SCORES + ((Leaderboard)i) + TimeScope.Today + j);
				}
			}
		}

		[MenuItem("Debug/Leaderboards/Print All My Leaderboard Scores")]
		private static void PrintAllMyLeaderboardScores()
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++) {
				Leaderboard l = (Leaderboard)i;
				if (Application.isPlaying)
					Debug.LogFormat("{0}: {1}", l, GetMyLeaderboardEntry(l));
				else
					Debug.LogFormat("{0}: {1}", l, PlayerPrefs.GetString(PREF_MY_LEADERBOARD_SCORE + ((Leaderboard)i).ToString()));
			}
		}

		[MenuItem("Debug/Leaderboards/Print All Top Scores")]
		private static void PrintAllTopScores()
		{
			for (int i = 0, imax = EnumUtils.GetCount<Leaderboard>(); i < imax; i++)
				PrintTopScores((Leaderboard)i);
		}

		private static void PrintTopScores(Leaderboard leaderboard)
		{
			if (Application.isPlaying) {
				Debug.LogFormat("All Time top scores of {0}:", leaderboard);
				foreach (LeaderboardEntry le in GetTopScores (leaderboard, TimeScope.AllTime))
					Debug.Log(le);
				Debug.LogFormat("Week top scores of {0}:", leaderboard);
				foreach (LeaderboardEntry le in GetTopScores (leaderboard, TimeScope.Week))
					Debug.Log(le);
				Debug.LogFormat("Today top scores of {0}:", leaderboard);
				foreach (LeaderboardEntry le in GetTopScores (leaderboard, TimeScope.Today))
					Debug.Log(le);
			} else {
				Debug.LogFormat("All Time top scores of {0}:", leaderboard);
				for (int i = 0, imax = LeaderboardManager.numLoadedTopScores; i < imax; i++)
					Debug.Log(PlayerPrefs.GetString(PREF_TOP_SCORES + leaderboard + TimeScope.AllTime + i));
				Debug.LogFormat("Week top scores of {0}:", leaderboard);
				for (int i = 0, imax = LeaderboardManager.numLoadedTopScores; i < imax; i++)
					Debug.Log(PlayerPrefs.GetString(PREF_TOP_SCORES + leaderboard + TimeScope.Week + i));
				Debug.LogFormat("Today top scores of {0}:", leaderboard);
				for (int i = 0, imax = LeaderboardManager.numLoadedTopScores; i < imax; i++)
					Debug.Log(PlayerPrefs.GetString(PREF_TOP_SCORES + leaderboard + TimeScope.Today + i));
			}
		}

		[MenuItem("Debug/Leaderboards/Print Parameters", false, 601)]
		private static void PrintParameters()
		{
			if (Application.isPlaying)
				Debug.Log("curTimeScope=" + currentTimeScope);
			else
				Debug.Log("curTimeScope=" + PlayerPrefs.GetInt(PREF_CURRENT_TIME_SCOPE));
			DebugUtils.LogEach(instance.leaderboardInfos, "leaderboardInfos");
		}

		#endif

	}

}