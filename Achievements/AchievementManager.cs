using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms;
using System.Collections;
using System.Collections.Generic;
using Prime31;

namespace SweatyChair
{

	public class AchievementManager : Singleton<AchievementManager>
	{

		public static event UnityAction<bool> uiToggledEvent;
		public static event UnityAction<AchievementGroupInfo> achievementChangedEvent;

		[UnityEngine.Serialization.FormerlySerializedAs("achievementInfos")]
		public AchievementGroupInfo[] achievementGroupInfos;

		// Load online and update achievements progress to local, only used if achievement prgress is not saved in GameSave
		public bool loadAchievementsOnStart = true;

		public bool debugMode = false;

		protected override void Awake() {
			base.Awake();

			if (loadAchievementsOnStart) {
				PlayGameCenterManager.authenticationSucceededEvent += LoadAchievement;
#if UNITY_IOS || UNITY_TVOS
				GameCenterManager.achievementsLoadedEvent += OnAchievementsLoaded;
#elif UNITY_ANDROID && !CHS
				GPGManager.reloadDataForKeySucceededEvent += OnAchievementsLoaded; // Check achivements sync
#endif
			}
		}

		public static bool hasReward {
			get {
				if (!instanceExists)
					return false;
				foreach (AchievementGroupInfo ai in instance.achievementGroupInfos) {
					if (ai.hasReward)
						return true;
				}
				return false;
			}
		}

		public static void OnAchievementChanged(AchievementGroupInfo achievementInfo) {
			if (achievementChangedEvent != null)
				achievementChangedEvent(achievementInfo);
		}

		private void LoadAchievement() {
#if UNITY_IOS || UNITY_TVOS
			// For new install with progress / multiple decies achievement sync
			GameCenterBinding.getAchievements();
#endif
		}

		/// <summary>
		/// Gets the achievement group infos.
		/// </summary>
		public static AchievementGroupInfo[] GetGroupInfos() {
			if (!instanceExists)
				return new AchievementGroupInfo[0];
			return instance.achievementGroupInfos;
		}

		/// <summary>
		/// Gets the ongoing achievement infos, ongoing means the most highest achievments which not yea completed or rewarded, for each achievement group.
		/// </summary>
		public static AchievementInfo[] GetOngoingInfos() {
			if (!instanceExists)
				return new AchievementInfo[0];
			AchievementInfo[] results = new AchievementInfo[instance.achievementGroupInfos.Length];
			for (int i = 0, imax = instance.achievementGroupInfos.Length; i < imax; i++)
				results[i] = instance.achievementGroupInfos[i].GetOngoingInfo();
			return results;
		}

		public static void Report(Achievement achievement, int totalOrIncrement = 1) {
			if (!instanceExists) {
				Debug.LogWarning("AchievementManager:Report - s_Instance=null");
				return;
			}

			if (instance.debugMode)
				Debug.LogFormat("AchievementManager:Report({0},{1})", achievement, totalOrIncrement);

			AchievementGroupInfo groupInfo = GetGroupInfo(achievement);
			if (groupInfo == null)
				return;

			// If not an incremental achievement and not total change, just return
			if (!groupInfo.isIncrement && groupInfo.currentCompleted == totalOrIncrement)
				return;

			groupInfo.AddOrSetCurrentCompleted(totalOrIncrement);

			OnAchievementChanged(groupInfo);

			// Report to GameCenter/PlayGames
			if (!PlayGameCenterManager.isAuthenticated)
				return;

#if UNITY_IOS || UNITY_TVOS

			for (int i = 0, imax = groupInfo.achievementInfos.Length; i < imax; i++) {
				if (instance.debugMode)
					Debug.LogFormat("AchievementManager:Report - currentCompleted={0}, requirements={1}", groupInfo.currentCompleted, groupInfo.achievementInfos[i].requirement);
				GameCenterBinding.reportAchievement(groupInfo.achievementInfos[i].iOSId, Mathf.Min(100f * groupInfo.currentCompleted / groupInfo.achievementInfos[i].requirement, 100));
			}

#elif UNITY_ANDROID && !CHS

			for (int i = 0, imax = groupInfo.achievementInfos.Length; i < imax; i++) {
				AchievementInfo info = groupInfo.achievementInfos[i];
				if (instance.debugMode)
					Debug.LogFormat("AchievementManager:Report - groupInfo.isIncrement={0}, info.requirement={1}, info.androidId={2}", groupInfo.isIncrement, info.requirement, info.androidId);
				if (groupInfo.isIncrement) { // Increment achievement
					if (info.requirement == 1) // Unlock directly if requirement is simply 1
						PlayGameServices.unlockAchievement(info.androidId);
					else // Increment the achievement by 1
						PlayGameServices.incrementAchievement(info.androidId, totalOrIncrement);
				} else { // Simple achievement
					if (groupInfo.currentCompleted >= info.requirement)
						PlayGameServices.unlockAchievement(info.androidId);
				}
			}

#endif
		}

#if UNITY_IOS || UNITY_TVOS
	
		private static void OnAchievementsLoaded(List<GameCenterAchievement> achievements)
		{
			if (instanceExists && instance.debugMode) {
				Debug.LogFormat("AchievementManager:OnAchievementsLoaded - achievements.Count={0}", achievements.Count);
				DebugUtils.Log(achievements);
			}

			// Sync online achievement progress to local (new install but old progress)
			foreach (GameCenterAchievement achievement in achievements) {
				foreach (AchievementGroupInfo ai in instance.achievementGroupInfos) {
				
					for (int i = 0, imax = ai.achievementInfos.Length; i < imax; i++) { // Loop throught all achievements pre-set
						if (achievement.identifier == ai.achievementInfos[i].iOSId) {

							int achievementCurrentCompleted = Mathf.RoundToInt(achievement.percentComplete / 100 * ai.achievementInfos[i].requirement);

							if (ai.currentCompleted < achievementCurrentCompleted - 2) // Allow 2 to be rounding error
							ai.currentCompleted = achievementCurrentCompleted;
						}
					}
				}
			}
		}

#elif UNITY_ANDROID && !CHS

		private static void OnAchievementsLoaded(string key) {
			if (key != "GPGModelAllAchievementMetadataKey") // The return data are all Play Games data returned, we only check for achievement
				return;

			if (!instanceExists)
				return;

			if (instance.debugMode)
				Debug.Log("AchievementManager:OnAchievementsLoaded()");

			List<GPGAchievementMetadata> achievementMetadatas = PlayGameServices.getAllAchievementMetadata();

			if (instance.debugMode) {
				Debug.LogFormat("AchievementManager:OnAchievementsLoaded - achievementMetadatas.Count={0}:", achievementMetadatas.Count);
				DebugUtils.Log(achievementMetadatas);
			}

			// Sync online achievement progress to local (new install but old progress), or local to online (offline progress)
			foreach (GPGAchievementMetadata achievementMetadata in achievementMetadatas) {
				foreach (AchievementGroupInfo groupInfo in instance.achievementGroupInfos) {
					for (int i = 0, imax = groupInfo.achievementInfos.Length; i < imax; i++) { // Loop throught all achievements pre-set

						AchievementInfo info = groupInfo.achievementInfos[i];

						if (achievementMetadata.achievementId == info.androidId) {
							if (groupInfo.isIncrement) { // Increment achievement

								if (groupInfo.currentCompleted > achievementMetadata.completedSteps)
									PlayGameServices.incrementAchievement(info.androidId, groupInfo.currentCompleted - achievementMetadata.completedSteps);
								else if (groupInfo.currentCompleted < achievementMetadata.completedSteps)
									groupInfo.currentCompleted = achievementMetadata.completedSteps;

							} else { // Simple achievement

								if (groupInfo.currentCompleted > info.requirement) // Unlock if cached high score larger than the requirement
									PlayGameServices.unlockAchievement(info.androidId);

							}
						}
					}
				}
			}
		}

#endif

		public static AchievementGroupInfo GetGroupInfo(int index) {
			return GetGroupInfo((Achievement)index);
		}

		public static AchievementGroupInfo GetGroupInfo(Achievement achievement) {
			if (!instanceExists)
				return null;

			int index = (int)achievement;

			if (index >= instance.achievementGroupInfos.Length) {
				Debug.LogFormat("AchievementManager:GetAchievementInfo - Invalid achievement={0}", achievement);
				return null;
			}

			if (instance.achievementGroupInfos[index].id != achievement)
				Debug.LogWarningFormat("AchievementManager:GetAchievementInfo - The {0}-th _achievementInfo.id != {1}", index, achievement);

			return instance.achievementGroupInfos[index];
		}

		public static void ToggleUI(bool doShow = true) {
			if (uiToggledEvent != null)
				uiToggledEvent(doShow);
		}

		public static void ShowPlayGameCenterAchievements() {
			if (!PlayGameCenterManager.isAuthenticated)
				PlayGameCenterManager.TryAuthentication(true);
#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.showAchievements();
#elif UNITY_ANDROID && !CHS
			PlayGameServices.showAchievements();
#endif
		}

#if UNITY_EDITOR

		[UnityEditor.MenuItem("Debug/Achievements/Print Achievement Infos")]
		private static void DebugPrintAchievementInfos() {
			DebugUtils.CheckPlaying(() => {
				if (instanceExists)
					instance.PrintAchievementInfos();
			});
		}

		[ContextMenu("Print Achievement Infos")]
		private void PrintAchievementInfos() {
			DebugUtils.LogEach(achievementGroupInfos, "achievementInfos");
		}

#endif

	}

}