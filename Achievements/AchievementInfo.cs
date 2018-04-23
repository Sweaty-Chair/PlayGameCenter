using UnityEngine;
using System.Collections.Generic;

namespace SweatyChair
{

	[System.Serializable]
	public struct AchievementInfo
	{

		// Name of the achievement, used for in-game UI only
		public string name;
		// Description of the achievement, used for in-game UI only
		public string description;
		// Required count for completing this achievement
		public int requirement;
		// Achievement ID setup in iTunesConnect console, format as: "com.sweatychair.gamename.achievementname"
		public string iOSId;
		// Achievement ID setup in Play Games console, format as: "CgkI9snButIFEAIQAw"
		public string androidId;
		// Reward used for in game achievements only
		public Reward reward;

		// Cache for getting the currentCompleted
		[System.NonSerialized]
		private AchievementGroupInfo _groupInfo;

		[System.NonSerialized]
		private int _index;

		public int currentCompleted {
			get {
				if (_groupInfo == null)
					return 0;
				return _groupInfo.currentCompleted;
			}
		}

		public bool isCompleted {
			get { return currentCompleted >= requirement; }
		}

		public float progress {
			get { return 1f * currentCompleted / requirement; }
		}

		public bool isRewarded {
			get {
				if (_groupInfo == null)
					return false;
				return _groupInfo.IsRewarded(_index);
			}
		}

		public void Reward()
		{
			_groupInfo.Reward(_index);
		}

		public void SetGroupInfo(AchievementGroupInfo groupInfo, int index)
		{
			_groupInfo = groupInfo;
			_index = index;
		}

		public override string ToString()
		{
			return string.Format("[AchievementInfo: name={0}, description={1}, requirement={2}, iOSid={3}, androidId={4}, reward={5}]", name, description, requirement, iOSId, androidId, reward);
		}

	}

}