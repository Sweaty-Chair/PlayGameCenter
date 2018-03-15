using UnityEngine.SocialPlatforms;
using Prime31;

// A generic score class derived from GameCenterScore/GPGScore so can use in BOTH platforms

namespace SweatyChair
{

	[System.Serializable]
	public class LeaderboardEntry
	{

		public string name;
		public int rank = 0;
		// Depend on the leaderboard setting, if score is a float then it would multiple 100 from the original, e.g. 19.99 -> 1999
		public long score = 0;

		public float floatScore { // i.e. scaled score back to original float score, e.g. 1999 -> 19.99
			get {
				if (score <= 0)
					return 0;
				return score / 100f;
			}
		}

		public string prefData {
			get { return name + "|" + rank + "|" + score; }
		}

		public LeaderboardEntry(string name, int rank, long score)
		{
			this.name = name;
			this.rank = rank;
			this.score = score;
		}

		public LeaderboardEntry(string[] rawDataArray)
		{
			name = rawDataArray[0];
			rank = int.Parse(rawDataArray[1]);
			score = int.Parse(rawDataArray[2]);
		}

		public LeaderboardEntry(LeaderboardEntry orig, int r)
		{
			name = orig.name;
			rank = r;
			score = orig.score;
		}

		public static bool CheckRawDataValid(string[] rawDataArray)
		{
			return rawDataArray != null && rawDataArray.Length >= 3;
		}

		#if UNITY_IOS
		
		public LeaderboardEntry (GameCenterScore score)
		{
			this.name = score.alias;
			this.rank = score.rank;
			this.score = (int)score.value;
		}
	
		#elif UNITY_ANDROID && !CHS

		public LeaderboardEntry(GPGScore score)
		{
			this.name = score.displayName;
			this.rank = (int)score.rank;
			this.score = (int)score.value;
		}

		#endif

		public override string ToString()
		{
			return string.Format("[LeaderboardEntry: name={0}, rank={1}, score={2}]", name, rank, score);
		}

	}

}