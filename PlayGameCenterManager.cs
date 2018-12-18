using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms;
using System.Collections;
using System.Collections.Generic;
using Prime31;

namespace SweatyChair
{

    public class PlayGameCenterManager : Singleton<PlayGameCenterManager>
    {

        private const string PREF_PREVIOUS_LOGINED = "PlayGameCenterPreviousLogined";

        // Events
        public static event UnityAction authenticationSucceededEvent;
        public static event UnityAction<string> playerNameLoadedEvent;

        // Avoid PlayerAutheticated run twice
        public static bool isAuthenticated = false;
        // Avoid keep trying login when offline or when player don't use Play Games
        private static bool isLastAuthenticationFailed = false;

        // Automatically login at start
        public bool loginOnStart = true;

        public bool debugMode = false;

        new protected virtual void Awake()
        {
            base.Awake();

#if UNITY_IOS || UNITY_TVOS

			GameCenterManager.playerAuthenticatedEvent += OnAuthenticationSucceeded;
			GameCenterManager.playerFailedToAuthenticateEvent += OnAutheticationFailed;

#elif UNITY_ANDROID && !CHS

			GPGManager.authenticationSucceededEvent += OnAuthenticationSucceeded;
			GPGManager.authenticationFailedEvent += OnAutheticationFailed;

#endif
        }

        void Start()
        {
#if UNITY_ANDROID && CHS
			return; // No PlayGameCenter for Android Chinese
#endif

            bool shouldLogin = false;
            if (loginOnStart)
            {
                if (PlayerPrefs.GetInt(PREF_PREVIOUS_LOGINED, 1) == 1)
                    shouldLogin = true;
                else // If previous failed login, skip login here
                    Debug.Log("PlayGameCenterManager:Start - cannot login before, skipping login now.");
            }
            else
            {
                if (PlayerPrefs.GetInt(PREF_PREVIOUS_LOGINED) == 1) // If previous succeed login, try login at launch
                    shouldLogin = true;
            }
            if (shouldLogin)
                TryAuthentication();
        }

        #region Authentication

        public static bool IsSingedIn()
        {
#if UNITY_IOS
			return GameCenterBinding.isPlayerAuthenticated();
#elif UNITY_ANDROID
			return PlayGameServices.isSignedIn();
#endif
            return false;
        }

        private static bool _isForcingAuthentication = false;

        public static void TryAuthentication(bool forceMode = false)
        {
            if (instanceExists && instance.debugMode)
                Debug.LogFormat("PlayGameCenterManager:TryAuthentication({0}) - isAuthenticated={1}, isLastAuthenticationFailed={2}", forceMode, isAuthenticated, isLastAuthenticationFailed);

			if (IsSingedIn()) // If already logined, skip here
                return;

			if (isLastAuthenticationFailed && !forceMode) // If last login failed, don't do it again (unless in forceMode)
                return;

            _isForcingAuthentication = forceMode;

#if UNITY_IOS
			GameCenterBinding.authenticateLocalPlayer(forceMode); // Authenticate the player
#elif UNITY_ANDROID && !CHS
			if (forceMode)
				PlayGameServices.authenticate(); // Authenticate with UI
			else
				PlayGameServices.attemptSilentAuthentication(); // Authenticate Silently (with no UI)
#endif
        }

#if UNITY_IOS || UNITY_TVOS
	
		private void OnAuthenticationSucceeded()
		{
			if (debugMode)
				Debug.LogFormat("PlayGameCenterManager:OnAuthenticationSucceeded()");

			ProcessAuthenticationSucceeded();

			playerNameLoadedEvent?.Invoke(GameCenterBinding.playerAlias());
		}

#endif

#if UNITY_ANDROID && !CHS
	
		private void OnAuthenticationSucceeded(string param)
		{
			if (debugMode)
				Debug.LogFormat("PlayGameCenterManager:OnAuthenticationSucceeded({0})");

			ProcessAuthenticationSucceeded();

			playerNameLoadedEvent?.Invoke(PlayGameServices.getLocalPlayerInfo().name);
		}

#endif

        private void ProcessAuthenticationSucceeded()
        {
            _isForcingAuthentication = false;
            PlayerPrefs.SetInt(PREF_PREVIOUS_LOGINED, 1);
            authenticationSucceededEvent?.Invoke();
        }

        private static void OnAutheticationFailed(string error)
        {
            PlayerPrefs.SetInt(PREF_PREVIOUS_LOGINED, 0);
            Debug.LogFormat("PlayGameCenterManager:OnAutheticationFailed({0})", error);
            isLastAuthenticationFailed = true;
#if UNITY_ANDROID
			if (_isForcingAuthentication) {
				new Message {
					title = LocalizeUtils.Get(TermCategory.Message, GlobalStrings.MSG_LOGIN_FAILED_TITLE),
					content = LocalizeUtils.Get(TermCategory.Message, "Please install the latest Google Play Games and try again."),
				}.Show();
			}
			_isForcingAuthentication = false;
#endif
        }

        #endregion

        public static string GetPlayerId()
        {
#if UNITY_IOS
			return GameCenterBinding.playerIdentifier();
#elif UNITY_ANDROID && !CHS
			return PlayGameServices.getLocalPlayerInfo().playerId;
#else
            return string.Empty;
#endif
        }

        public static string GetPlayerName()
        {
#if UNITY_IOS
			return GameCenterBinding.playerAlias();
#elif UNITY_ANDROID && !CHS
			return PlayGameServices.getLocalPlayerInfo().name;
#else
            return string.Empty;
#endif
        }

        #region Show Game Center / Play Games UI

        // Show a custom notification banner, iOS only
        public static void ShowCustomNotificationBanner(string title, string message, float duration)
        {
#if UNITY_IOS || UNITY_TVOS
			GameCenterBinding.showCustomNotificationBanner(title, message, duration);
#endif
        }

        #endregion

    }

}