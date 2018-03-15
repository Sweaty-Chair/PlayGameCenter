using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms;
using System.Collections;
using System.Collections.Generic;
using Prime31;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SweatyChair
{

	public class AchievementManager : Singleton<AchievementManager>
	{

		public static event UnityAction<bool> uiToggledEvent;
		public static event UnityAction<AchievementInfo> achievementChangedEvent;

		public AchievementInfo[] achievementInfos;

		// Load online and update achievements progress to local, only used if achievement prgress is not saved in GameSave
		public bool loadAchievementsOnStart = true;

		public bool debugMode = false;

		protected override void Awake()
		{
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
				if (s_Instance == null)
					return false;
				foreach (AchievementInfo ai in s_Instance.achievementInfos) {
					if (ai.hasReward)
						return true;
				}
				return false;
			}
		}

		public static void OnAchievementChanged(AchievementInfo achievementInfo)
		{
			if (achievementChangedEvent != null)
				achievementChangedEvent(achievementInfo);
		}

		private void LoadAchievement()
		{
			#if UNITY_IOS || UNITY_TVOS
			// For new install with progress / multiple decies achievement sync
			GameCenterBinding.getAchievements();
			#endif
		}

		public static AchievementInfo[] GetInfos()
		{
			if (s_InstanceExists)
				return s_Instance.achievementInfos;
			return new AchievementInfo[0];
		}

		public static void Report(Achievement achievement, int totalOrIncrement = 1)
		{
			if (!s_InstanceExists) {
				Debug.LogWarning("AchievementManager:Report - s_Instance=null");
				return;
			}

			if (s_Instance.debugMode)
				Debug.LogFormat("AchievementManager:Report({0},{1})", achievement, totalOrIncrement);

			AchievementInfo ai = GetAchievementInfo(achievement);
			if (ai == null)
				return;

			// If not an incremental achievement and not total change, just return
			if (!ai.isIncrement && ai.currentCompleted == totalOrIncrement)
				return;

			ai.AddOrSetCompleted(totalOrIncrement);

			OnAchievementChanged(ai);

			// Report to GameCenter/PlayGames
			if (!PlayGameCenterManager.isAuthenticated)
				return;
		
			#if UNITY_IOS || UNITY_TVOS
			for (int i = 0, imax = ai.requirements.Length; i < imax; i++) {
				if (s_Instance.debugMode)
					Debug.LogFormat("AchievementManager:Report - currentCompleted={0}, requirements={1}", ai.currentCompleted, ai.requirements[i]);
				GameCenterBinding.reportAchievement(ai.iOSIds[i], Mathf.Min(100f * ai.currentCompleted / ai.requirements[i], 100));
			}
			#elif UNITY_ANDROID && !CHS
			for (int i = 0, imax = ai.requirements.Length; i < imax; i++) {
				if (s_Instance.debugMode)
					Debug.LogFormat("AchievementManager:Report - ai.isIncrement={0}, ai.requirements[i]={1}, ai.androidIds[i]={2}", ai.isIncrement, ai.requirements[i], ai.androidIds[i]);
				if (ai.isIncrement) { // Increment achievement
					if (ai.requirements[i] == 1) // Unlock directly if requirement is simply 1
						PlayGameServices.unlockAchievement(ai.androidIds[i]);
					else // Increment the achievement by 1
						PlayGameServices.incrementAchievement(ai.androidIds[i], totalOrIncrement);
				} else { // Simple achievement
					if (ai.currentCompleted >= ai.requirements[i])
						PlayGameServices.unlockAchievement(ai.androidIds[i]);
				}
			}
			#endif
		}

		#if UNITY_IOS || UNITY_TVOS
	
		private static void OnAchievementsLoaded(List<GameCenterAchievement> achievements)
		{
			if (s_InstanceExists && s_Instance.debugMode) {
				Debug.LogFormat("AchievementManager:OnAchievementsLoaded - achievements.Count={0}:", achievements.Count);
				DebugUtils.Log(achievements);
			}

			// Sync online achievement progress to local (new install but old progress)
			foreach (GameCenterAchievement achievement in achievements) {
				foreach (AchievementInfo ai in s_Instance.achievementInfos) {
				
					for (int i = 0, imax = ai.requirements.Length; i < imax; i++) { // Loop throught all achievements pre-set
						if (achievement.identifier == ai.iOSIds[i]) {

							int achievementCurrentCompleted = Mathf.RoundToInt(achievement.percentComplete / 100 * ai.requirements[i]);

							if (ai.currentCompleted < achievementCurrentCompleted - 2) // Allow 2 to be rounding error
							ai.currentCompleted = achievementCurrentCompleted;
						}
					}
				}
			}
		}

		#elif UNITY_ANDROID && !CHS
	
		private static void OnAchievementsLoaded(string key)
		{
			if (key != "GPGModelAllAchievementMetadataKey") // The return data are all Play Games data returned, we only check for achievement
				return;

			if (!s_InstanceExists)
				return;

			if (s_Instance.debugMode)
				Debug.Log("AchievementManager:OnAchievementsLoaded()");

			List<GPGAchievementMetadata> achievementMetadatas = PlayGameServices.getAllAchievementMetadata();

			if (s_Instance.debugMode) {
				Debug.LogFormat("AchievementManager:OnAchievementsLoaded - achievementMetadatas.Count={0}:", achievementMetadatas.Count);
				DebugUtils.Log(achievementMetadatas);
			}

			// Sync online achievement progress to local (new install but old progress), or local to online (offline progress)
			foreach (GPGAchievementMetadata achievementMetadata in achievementMetadatas) {
				foreach (AchievementInfo ai in s_Instance.achievementInfos) {
					for (int i = 0, imax = ai.requirements.Length; i < imax; i++) { // Loop throught all achievements pre-set
						
						if (achievementMetadata.achievementId == ai.androidIds[i]) {
							if (ai.isIncrement) { // Increment achievement

								if (ai.currentCompleted > achievementMetadata.completedSteps)
									PlayGameServices.incrementAchievement(ai.androidIds[i], ai.currentCompleted - achievementMetadata.completedSteps);
								else if (ai.currentCompleted < achievementMetadata.completedSteps)
									ai.currentCompleted = achievementMetadata.completedSteps;

							} else { // Simple achievement

								if (ai.currentCompleted > ai.requirements[i]) // Unlock if cached high score larger than the requirement
									PlayGameServices.unlockAchievement(ai.androidIds[i]);

							}
						}
					}
				}
			}
		}

		#endif

		public static AchievementInfo GetAchievementInfo(int index)
		{
			return GetAchievementInfo((Achievement)index);
		}

		public static AchievementInfo GetAchievementInfo(Achievement achievement)
		{
			if (s_Instance == null)
				return null;

			int index = (int)achievement;

			if (index >= s_Instance.achievementInfos.Length) {
				Debug.LogFormat("AchievementManager:GetAchievementInfo - Invalid achievement={0}", achievement);
				return null;
			}

			if (s_Instance.achievementInfos[index].id != achievement)
				Debug.LogWarningFormat("AchievementManager:GetAchievementInfo - The {0}-th _achievementInfo.id != {1}", index, achievement);

			return s_Instance.achievementInfos[index];
		}

		public static void ToggleUI(bool doShow = true)
		{
			if (uiToggledEvent != null)
				uiToggledEvent(doShow);
		}

		public static void ShowPlayGameCenterAchievements()
		{
			if (!PlayGameCenterManager.isAuthenticated)
				PlayGameCenterManager.TryAuthentication(true);
			#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.showAchievements();
			#elif UNITY_ANDROID && !CHS
			PlayGameServices.showAchievements();
			#endif
		}

		#if UNITY_EDITOR

		[ContextMenu("Print Achievement Infos")]
		private void PrintAchievementInfos()
		{
			DebugUtils.LogEach(achievementInfos, "achievementInfos");
		}

		[MenuItem("Debug/Achievements/Print Achievement Infos")]
		private static void DebugPrintAchievementInfos()
		{
			if (DebugUtils.CheckPlaying() && s_InstanceExists)
				s_Instance.PrintAchievementInfos();
		}

		#endif

	}

}