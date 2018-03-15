using UnityEngine;
using System.Collections.Generic;

namespace SweatyChair
{

	[System.Serializable]
	public class AchievementInfo
	{

		private const string GS_ACHIEVEMENT_PROGRESSES = "achievementProgresses";
		private const string GS_ACHIEVEMENT_REWARDED_INDEXES = "achievementRewardedIndexes";
		// + AchievementName, e.g. GameCenterPlayGamesAchievementTotalDistance, GameCenterPlayGamesAchievementTotalNumMutation
		private const string PREF_ACHIEVEMENT_CURRENT_COMPLETED = "PlayGameCenterAchievementCurrentCompleted";

		public Achievement id;
		public string name;
		public string description;

		public bool isIncrement = false;
		public bool isInGame = true;

		public int[] requirements = new int[0];
		public string[] iOSIds = new string[0];
		public string[] androidIds = new string[0];
		// Reward used for in game achievements only
		public Reward[] rewards = new Reward[0];

		public int currentCompleted {
			get {
				if (isInGame) { // Use Game Save for in game achievement
					Dictionary<int, int> dict = GameSaveManager.GetIntDictionary(GS_ACHIEVEMENT_PROGRESSES);

					// TODO: backward compatiable - 1.0.4
					if (PlayerPrefs.HasKey(PREF_ACHIEVEMENT_CURRENT_COMPLETED + id)) {
						dict[(int)id] = PlayerPrefs.GetInt(PREF_ACHIEVEMENT_CURRENT_COMPLETED + id);
						PlayerPrefs.DeleteKey(PREF_ACHIEVEMENT_CURRENT_COMPLETED + id);
					}
					// End TODO

					if (dict.ContainsKey((int)id))
						return dict[(int)id];
					return 0;
				} else {
					return PlayerPrefs.GetInt(PREF_ACHIEVEMENT_CURRENT_COMPLETED + id);
				}
			}
			set {
				if (isInGame) // Use Game Save for in game achievement
				GameSaveManager.SetIntDictionary(GS_ACHIEVEMENT_PROGRESSES, (int)id, value);
				else
					PlayerPrefs.SetInt(PREF_ACHIEVEMENT_CURRENT_COMPLETED + id, value);
			}
		}

		// The rewarded index, -1 if never rewarded
		public int rewardedIndex {
			get {
				Dictionary<int, int> dict = GameSaveManager.GetIntDictionary(GS_ACHIEVEMENT_REWARDED_INDEXES);
				if (dict.ContainsKey((int)id))
					return dict[(int)id];
				return -1;
			}
			set { GameSaveManager.SetIntDictionary(GS_ACHIEVEMENT_REWARDED_INDEXES, (int)id, value); }
		}

		public bool hasReward {
			get {
				for (int i = 0, imax = requirements.Length; i < imax; i++) {
					if (!IsCompleted(i))
						return false;
					if (i > rewardedIndex)
						return true;
				}
				return false;
			}
		}

		// Return true if any new completed, for in game reward only
		public void AddOrSetCompleted(int totalOrIncrement = 1)
		{
			if (isIncrement)
				currentCompleted += totalOrIncrement;
			else
				currentCompleted = totalOrIncrement;
		}

		public bool IsCompleted(int index)
		{
			if (index >= requirements.Length)
				return false;
			return currentCompleted >= requirements[index];
		}

		public bool IsRewarded(int index)
		{
			return rewardedIndex >= index;
		}

		public override string ToString()
		{
			return string.Format("[AchievementInfo: id={0}, name={1}, isIncrement={2}, isInGame={3}, requirementArray={4}, currentCompleted={5}, rewardedIndex={6}]",
				id,
				name,
				isIncrement,
				isInGame,
				StringUtils.ArrayToString(requirements),
				currentCompleted,
				rewardedIndex
			);
		}

	}

}