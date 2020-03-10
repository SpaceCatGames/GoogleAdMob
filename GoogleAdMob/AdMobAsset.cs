using System;
using GoogleMobileAds.Api;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace SpaceCatGames.OpenSource
{
    /// <summary>
    /// Instruction for use:
    /// <para>
    /// Subscribe at callbacks
    /// e.g. OnWatchedCallback += e => Dispatcher.Instance.Enqueue( () => Foo() );    
    /// </para> 
    /// Call LoadAd(), or set InitOnEnable flag.
    /// Call LoadAd() any time or set RequestNewAfterPlay flag.
    /// <para>
    /// Then call Play() or set PlayAfterLoad flag.
    /// </para>
    /// <para>
    ///  Not combine inconsistent fields for correct behavior. 
    /// </para>
    /// You are the best! 
    /// </summary>
    [CreateAssetMenu( fileName = "AdMobAsset", menuName = "SpaceCatGames/AdMobAsset" )]
    public class AdMobAsset : ScriptableObject
    {
#if UNITY_EDITOR
        public const double EditorAmount = 10f;
        public const string EditorType = "Editor";
#endif
        
        [Header("Behaviour")]
        [Tooltip( "Will LoadAd() invoked at OnEnable?" )]
        public bool InitOnEnable;
        [Tooltip( "Will LoadAd() invoked on load/show failed?\n" +
                  "Recommended using with checking network connectivity or not use with InitOnEnable." )]
        public bool InitOnFailed;
        [Tooltip( "Ad will be unload if asset disabled." )]
        public bool UnloadOnDisable = true;
        [Tooltip( "Will LoadAd() invoked again after Play?" )]
        public bool RequestNewAfterPlay = true;
        [Tooltip("Show ad after loaded.\n" +
                 "DON'T USE with RequestNewAfterPlay!")]
        public bool PlayAfterLoad;

        [Header( "Debug" )]
        [Tooltip( "False - ads is test, true - your id" )]
        public bool IsRealAds;
        [Tooltip( "If true, called AddTestDevice(uniqueID)" )]
        public bool IsTest = true;

        [Header("Options")]
        [Tooltip( "Type of ads" )]
        public AdType AdType;

        /// <summary> ApplicationID for Android (PlayMarket) </summary>
        [Tooltip( "ApplicationID for Android (PlayMarket)" )]
        public string PlayMarketId;
        /// <summary> ApplicationID for iOS (AppStore) </summary>
        [Tooltip( "ApplicationID for iOS (AppStore)" )]
        public string AppStoreId;

        /// <summary> Ad size for BannerView type </summary>
        public AdSize AdSize = AdSize.SmartBanner;
        /// <summary> Ad position for BannerView type </summary>
        [SerializeField]
#if ODIN_INSPECTOR
        [ShowIf( "AdType", AdType.BannerView )]
#endif  
        [Tooltip( "Ad position for BannerView type" )]
        protected AdPosition AdPosition = AdPosition.Center;

        private RewardedAd rewardedAd;
        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private string currentAdVideoId;

        /// <summary>
        /// Unique device ID
        /// </summary>
        private static string UniqueID;

         /// <summary> Invoked on loaded </summary>
        public event Action OnLoadedCallback;

        /// <summary> Invoked on failed (load or show), arg is message </summary>
        public event Action<string> OnFailedCallback;

        /// <summary> Only for RewardedAd type, arg is <see cref="Amount"/> </summary>
        public event Action<double> OnWatchedCallback;

        /// <summary> Invoked when ad is closed </summary>
        public event Action OnClosedCallback;

        /// <summary> Amount for RewardedAd </summary>
        public double Amount { get; set; }

        /// <summary> Current ad object of type </summary>
        public object AdObject
        {
            get
            {
                // C# version not for ConvertSwitchStatementToSwitchExpression
                // ReSharper disable once ConvertSwitchStatementToSwitchExpression
                switch ( AdType )
                {
                    case AdType.RewardVideoAd:
                        return rewardedAd;
                    case AdType.BannerView:
                        return bannerView;
                    case AdType.InterstitialAd:
                        return interstitialAd;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary> Was ad initialized? </summary>
        public bool IsInitialized => AdObject != null;

        /// <summary> Is ad loading now? </summary>
        public bool IsLoading { get; private set; }

        /// <summary> Is ad loaded and ready for play? </summary>
        public bool IsLoaded
        {
            get
            {
                if ( !IsInitialized )
                    return false;

                switch ( AdType )
                {
                    case AdType.RewardVideoAd:
                        return rewardedAd.IsLoaded();
                    case AdType.BannerView:
                        return true;
                    case AdType.InterstitialAd:
                        return interstitialAd.IsLoaded();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected void OnDisable()
        {
            if ( !UnloadOnDisable )
                    return;

            if ( Application.isPlaying )
                Debug.Log( $"AdMob asset {name} was disabled. Ad unloaded." );
            rewardedAd = null;
            bannerView = null;
            interstitialAd = null;
        }

        protected void OnEnable()
        {
#if UNITY_EDITOR
            if ( !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode )
                return;
#elif DEBUG
            IsTest = true;
#endif

            if ( Application.installMode == ApplicationInstallMode.Store )
            {
                IsRealAds = true;
                IsTest = false;
            }

            if ( InitOnEnable )
            {
                LoadAd();
            }

            if ( IsTest && UniqueID == null )
            {
                UniqueID = SystemInfo.deviceUniqueIdentifier;
            }
        }

        /// <summary>
        /// Ad initialization and loading
        /// </summary>
        public void LoadAd()
        {
            IsLoading = true;
            currentAdVideoId = GetVideoID();
            switch ( AdType )
            {
                case AdType.RewardVideoAd:
                    InitRewardVideoAd();
                    break;
                case AdType.BannerView:
                    InitBannerView();
                    break;
                case AdType.InterstitialAd:
                    InitInterstitialAd();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Check IsLoaded before then call this method
        /// </summary>
        public void Play()
        {
            switch ( AdType )
            {
                case AdType.RewardVideoAd:
                    rewardedAd.Show();
                    break;
                case AdType.BannerView:
                    bannerView.Show();
                    break;
                case AdType.InterstitialAd:
                    interstitialAd.Show();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if ( RequestNewAfterPlay )
                 LoadAd();

#if UNITY_EDITOR
            HandleUserEarnedReward( AdObject,
                new Reward { Amount = EditorAmount, Type = EditorType } );
#endif
        }

        /// <summary>
        /// Hides the BannerView from the screen
        /// </summary>
        public void BannerHide()
        {
            if ( AdType == AdType.BannerView )
            {
                bannerView.Hide();
            }
        }

        /// <summary>
        /// Request for load ad
        /// </summary>
        private void Request()
        {
            var builder = new AdRequest.Builder();
            if ( IsTest )
            {
                 builder
                    .AddTestDevice( AdRequest.TestDeviceSimulator )
                    .AddTestDevice( UniqueID );
            }

            var adRequest = builder.Build();
            
            switch ( AdType )
            {
                case AdType.RewardVideoAd:
                    rewardedAd.LoadAd( adRequest );
                    break;
                case AdType.BannerView:
                    bannerView.LoadAd( adRequest );
                    break;
                case AdType.InterstitialAd:
                    interstitialAd.LoadAd( adRequest );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private string GetVideoID()
        {
#if UNITY_EDITOR
            return string.Empty;
#elif UNITY_ANDROID
            if ( IsRealAds )
                return PlayMarketId;

            switch ( AdType )
            {
                case AdType.RewardVideoAd:
                    return "ca-app-pub-3940256099942544/5224354917";
                case AdType.BannerView:
                    return "ca-app-pub-3940256099942544/6300978111";
                case AdType.InterstitialAd:
                    return "ca-app-pub-3940256099942544/1033173712";
                default:
                    return "ca-app-pub-3940256099942544/2247696110";
            }

#elif UNITY_IPHONE
            if ( IsRealAds )
                return AppStoreId;

            switch ( AdType )
            {
                case AdType.RewardVideoAd:
                    return "ca-app-pub-3940256099942544/1712485313";
                case AdType.BannerView:
                    return "ca-app-pub-3940256099942544/2934735716";
                case AdType.InterstitialAd:
                    return "ca-app-pub-3940256099942544/4411468910";
                default:
                    return "ca-app-pub-3940256099942544/3986624511";
            }

#else
            return string.Empty;
#endif
        }

#region Init

        private void InitRewardVideoAd()
        {
            rewardedAd = new RewardedAd( currentAdVideoId );

            rewardedAd.OnUserEarnedReward += HandleUserEarnedReward;
            rewardedAd.OnAdOpening += HandleUserOpening;
            rewardedAd.OnAdLoaded += HandleUserLoaded;
            rewardedAd.OnAdFailedToLoad += HandleUserFailed;
            rewardedAd.OnAdFailedToShow += HandleUserFailed;
            rewardedAd.OnAdClosed += HandleRewardedAdClosed;

            Request();
        }       

        private void InitBannerView()
        {
            bannerView = new BannerView( currentAdVideoId, AdSize, AdPosition );

            bannerView.OnAdFailedToLoad += HandleUserFailed;
            bannerView.OnAdOpening += HandleUserOpening;
            bannerView.OnAdLoaded += HandleUserLoaded;
            bannerView.OnAdLeavingApplication += HandleLeavingApplication;
            bannerView.OnAdClosed += HandleRewardedAdClosed;

            Request();
        }

        private void InitInterstitialAd()
        {
            interstitialAd = new InterstitialAd( currentAdVideoId );

            interstitialAd.OnAdFailedToLoad += HandleUserFailed;
            interstitialAd.OnAdOpening += HandleUserOpening;
            interstitialAd.OnAdLoaded += HandleUserLoaded;
            interstitialAd.OnAdLeavingApplication += HandleLeavingApplication;
            interstitialAd.OnAdClosed += HandleRewardedAdClosed;

            Request();
        }

#endregion

#region Video callbacks

        private void HandleUserOpening( object sender, EventArgs e )
        {
           
        }

        private void HandleUserLoaded( object sender, EventArgs e )
        {
            IsLoading = false;
            OnLoadedCallback?.Invoke();

            if ( PlayAfterLoad )
            {
                Play();
            }
        }

        private void HandleRewardedAdClosed( object sender, EventArgs e )
        {
            Debug.Log( "Ad watching: CLOSED" );

            OnClosedCallback?.Invoke();
        }

        private void HandleUserEarnedReward( object sender, Reward e )
        {
            Debug.Log( $"Ad watching: SUCCESS {e.Type}: {e.Amount}" );

            Amount = e.Amount;
            OnWatchedCallback?.Invoke( Amount );
        }

        private void HandleUserFailed( object sender, AdErrorEventArgs e )
        {
            Debug.Log( $"Ad loading: FAILED. Message: {e.Message}" );

            IsLoading = false;
            Amount = 0;
            OnFailedCallback?.Invoke( e.Message );
            
            if ( InitOnFailed )
            {
                LoadAd();
            }
        }

        private void HandleUserFailed( object sender, AdFailedToLoadEventArgs e )
        {
            Debug.Log( $"Ad loading: FAILED. Message: {e.Message}" );
            
            IsLoading = false;
            Amount = 0;
            OnFailedCallback?.Invoke( e.Message );
            
            if ( InitOnFailed )
            {
                LoadAd();
            }
        }

        private void HandleLeavingApplication( object sender, EventArgs e )
        {
            Debug.Log( "Ad loading: APPLICATION LEAVE" );

            Amount = 0;
        }

#endregion
    }

    public enum AdType
    {
        RewardVideoAd = 0,
        BannerView = 1,
        InterstitialAd = 2
    }
}
