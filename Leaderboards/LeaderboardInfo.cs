using UnityEngine;

namespace SweatyChair
{

	[System.Serializable]
	public class LeaderboardInfo
	{

		// + LeaderboardName, e.g. PlayGameCenterLeaderboardCurrentScoreTotalWinMatch
		private const string PREF_LEADERBOARD_CURRENT_SCORE = "LeaderboardCurrentScore";

		public Leaderboard id;
		public string iOSId;
		public string androidId;

		public bool isIncrement = false;

		// For incremental leaderboard only
		public long currentScore {
			get { return (long)PlayerPrefs.GetInt(PREF_LEADERBOARD_CURRENT_SCORE + leaderboardId); }
			set { PlayerPrefs.SetInt(PREF_LEADERBOARD_CURRENT_SCORE + leaderboardId, (int)value); }
		}

		public string leaderboardId {
			get {
				#if UNITY_IOS || UNITY_TVOS
				return iOSId;
				#endif
				#if UNITY_ANDROID
				return androidId;
				#else
				return "";
				#endif
			}
		}

		public override string ToString()
		{
			return string.Format("[LeaderboardInfo: id={0}, iOSId={1}, androidId={2}, currentScore={3}]",
				leaderboardId,
				iOSId,
				androidId,
				currentScore);
		}

	}

}