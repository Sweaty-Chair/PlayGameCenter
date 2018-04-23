using UnityEngine;
using System.Collections.Generic;

namespace SweatyChair
{

	/// <summary>
	/// Achievement group info, containing a array of achievement infos, which having a similiar condition and required to complete in order.
	/// E.g. Contain kill 1 enemy, kill 10 enemies, kill 100 enemies, etc.
	/// </summary>

	[System.Serializable]
	public class AchievementGroupInfo
	{

		// GameSave key for current completed progress, for isInGame only
		private const string GS_ACHIEVEMENT_PROGRESSES = "AchievementGroupProgresses";
		// GameSave key for current rewarded indexes, for isInGame only
		private const string GS_ACHIEVEMENT_REWARDED_INDEXES = "AchievementGroupRewardedIndexes";

		// PlayerPrefs key for saving current completed progress, for Non-isInGame only
		// + AchievementName, e.g. AchievementGroupCurrentCompletedTotalDistance, AchievementGroupCurrentCompletedTotalNumMutation
		private const string PREF_ACHIEVEMENT_PROGRESSES = "AchievementGroupProgresses";

		// Achievement enum
		public Achievement id;
		// Name of the achievement group, used for in-game UI only
		public string name;
		// Description of the achievement group, used for in-game UI only
		public string description;

		// Are the achievements increment, such as killed enemy, played game; Otherwise a fixed number, such as highscore, collected coins in ONE game
		public bool isIncrement = false;
		// Are the achievements in-game and have reward when completing them
		public bool isInGame = true;

		// The children achievements that this group has
		public AchievementInfo[] achievementInfos = new AchievementInfo[0];

		#region Current Completed

		public int currentCompleted {
			get {
				if (isInGame) { // Use Game Save for in game achievement
					Dictionary<int, int> dict = GameSave.GetIntDictionary(GS_ACHIEVEMENT_PROGRESSES);
					if (dict.ContainsKey((int)id))
						return dict[(int)id];
					return 0;
				} else {
					return PlayerPrefs.GetInt(PREF_ACHIEVEMENT_PROGRESSES + id);
				}
			}
			set {
				if (isInGame) // Use Game Save for in game achievement
					GameSave.SetIntDictionary(GS_ACHIEVEMENT_PROGRESSES, (int)id, value);
				else
					PlayerPrefs.SetInt(PREF_ACHIEVEMENT_PROGRESSES + id, value);
			}
		}

		// Return true if any new completed, for in game reward only
		public void AddOrSetCurrentCompleted(int totalOrIncrement = 1)
		{
			if (isIncrement)
				currentCompleted += totalOrIncrement;
			else
				currentCompleted = totalOrIncrement;
		}

		public bool IsCompleted(int index)
		{
			if (index >= achievementInfos.Length)
				return false;
			return currentCompleted >= achievementInfos[index].requirement;
		}

		#endregion

		#region Rewards

		// The rewarded index, -1 if never rewarded
		public int rewardedIndex {
			get {
				Dictionary<int, int> dict = GameSave.GetIntDictionary(GS_ACHIEVEMENT_REWARDED_INDEXES);
				if (dict.ContainsKey((int)id))
					return dict[(int)id];
				return -1;
			}
			set { GameSave.SetIntDictionary(GS_ACHIEVEMENT_REWARDED_INDEXES, (int)id, value); }
		}

		public bool hasReward {
			get {
				for (int i = 0, imax = achievementInfos.Length; i < imax; i++) {
					if (!IsCompleted(i))
						return false;
					if (i > rewardedIndex)
						return true;
				}
				return false;
			}
		}

		public bool IsRewarded(int index)
		{
			return rewardedIndex >= index;
		}

		public void Reward(int index)
		{
			if (IsRewarded(index)) {
				Debug.LogErrorFormat("AchievementGroupInfo:Reward - Already rewarded for index={0}.", index);
				return;
			}
			if (index >= achievementInfos.Length) {
				Debug.LogErrorFormat("AchievementGroupInfo:Reward - Invalid index={0}.", index);
				return;
			}
			if (achievementInfos[index].reward == null) {
				Debug.LogErrorFormat("AchievementGroupInfo:Reward - Reward not set for index={0}.", index);
				return;
			}
			if (!achievementInfos[index].reward.CheckObtainable()) {
				Debug.LogErrorFormat("AchievementGroupInfo:Reward - Reward not obtainable for index={0}.", index);
				return;
			}
//			achievementInfos[index].reward.Obtain();
			achievementInfos[index].reward.Claim();
			rewardedIndex = index;
		}

		#endregion

		public AchievementInfo GetOngoingInfo()
		{
			for (int i = 0, imax = achievementInfos.Length; i < imax; i++) {
				achievementInfos[i].SetGroupInfo(this, i);
				if (!IsCompleted(i) || !IsRewarded(i))
					return achievementInfos[i];
			}
			return achievementInfos[achievementInfos.Length - 1];
		}

		public override string ToString()
		{
			return string.Format("[AchievementGroupInfo: id={0}, name={1}, isIncrement={2}, isInGame={3}, requirementArray={4}, currentCompleted={5}, rewardedIndex={6}]",
				id,
				name,
				isIncrement,
				isInGame,
				StringUtils.ArrayToString(achievementInfos),
				currentCompleted,
				rewardedIndex
			);
		}

		#if UNITY_EDITOR

		public string ToProgressString()
		{
			return string.Format("[AchievementGroupInfo: currentCompleted={0}, rewardedIndex={1}]",
				currentCompleted,
				rewardedIndex
			);
		}

		public void CompleteNext()
		{
			for (int i = 0, imax = achievementInfos.Length; i < imax; i++) {
				if (!IsCompleted(i)) {
					currentCompleted = achievementInfos[i].requirement;
					return;
				}
			}
			Debug.Log("AchievementGroupInfo:CompleteNext - All achievements completed.");
		}

		public void RewardNext()
		{
			for (int i = 0, imax = achievementInfos.Length; i < imax; i++) {
				if (IsCompleted(i) && !IsRewarded(i)) {
					Reward(i);
					return;
				}
			}
			Debug.Log("AchievementGroupInfo:RewardNext - All achievements rewarded.");
		}

		public void ResetProgress()
		{
			currentCompleted = 0;
			rewardedIndex = -1;
		}

		#endif

	}

}