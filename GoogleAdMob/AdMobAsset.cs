using System;
using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

namespace SpaceCatGames.OpenSource.Ads
{
    /// <summary>
    /// Usage:
    /// Create new asset, select ad type, add your ID.
    /// <para>
    /// Subscribe at callbacks
    /// OnWatchedCallback += e => MainThreadDispatcher.Instance.Enqueue( () => Foo() );
    /// or OnClosedCallback
    /// </para> 
    /// Call InitAd(), or set InitOnEnable flag.
    /// Call Request() any time (called into InitAd) or set true RequestNewAfterPlay flag.
    /// <para>
    /// Then call Play().
    /// </para>
    /// You are the best! 
    /// </summary>
    [CreateAssetMenu( fileName = "AdMobAsset", menuName = "SpaceCatGames/AdMobAsset" )]
    public class AdMobAsset : ScriptableObject
    {
        [Tooltip( "False - ads is test, true - your id" )]
        public bool IsRealAds;
        [Tooltip( "If true, called AddTestDevice(uniqueID)" )]
        public bool IsTest = true;
        [Tooltip( "Will InitAd() invoked at OnEnable?" )]
        public bool InitOnEnable;
        [Tooltip( "Will Request() invoked again after Play?" )]
        public bool RequestNewAfterPlay = true;

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
        [Sirenix.OdinInspector.ShowIf( "AdType", AdType.BannerView )]
#endif
        [Tooltip( "Ad position for BannerView type" )]
        protected AdPosition AdPosition = AdPosition.Center;

        private RewardedAd rewardedAd;
        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private string currentAdVideoId;

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

        /// <summary> Is ad initialized? </summary>
        public bool IsInitialized => AdObject != null;

        /// <summary> Is ad loaded? </summary>
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
#endif

#if DEBUG
            IsTest = true;
#endif

            if ( Application.installMode == ApplicationInstallMode.Store )
            {
                IsRealAds = true;
                IsTest = false;
            }

            if ( InitOnEnable )
            {
                InitAd();
            }
        }

        public void InitAd()
        {
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
                Request();

#if UNITY_EDITOR
            HandleUserEarnedReward( AdObject,
                new Reward { Amount = 10d, Type = "Editor" } );
#endif
        }

        public void Request()
        {
            var builder = new AdRequest.Builder();
            if ( IsTest )
            {
                builder
                    .AddTestDevice( AdRequest.TestDeviceSimulator )
                    .AddTestDevice( SystemInfo.deviceUniqueIdentifier );
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
            rewardedAd.OnAdFailedToLoad += HandleUserFailed;
            rewardedAd.OnAdFailedToShow += HandleUserFailed;
            rewardedAd.OnAdClosed += HandleRewardedAdClosed;

            Request();
        }

        private void InitBannerView()
        {
            bannerView = new BannerView( currentAdVideoId, AdSize, AdPosition );

            bannerView.OnAdFailedToLoad += HandleUserFailed;
            bannerView.OnAdLeavingApplication += HandleLeavingApplication;
            bannerView.OnAdClosed += HandleRewardedAdClosed;

            Request();
        }

        private void InitInterstitialAd()
        {
            interstitialAd = new InterstitialAd( currentAdVideoId );

            interstitialAd.OnAdFailedToLoad += HandleUserFailed;
            interstitialAd.OnAdLeavingApplication += HandleLeavingApplication;
            interstitialAd.OnAdClosed += HandleRewardedAdClosed;

            Request();
        }

#endregion

#region Video callbacks

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

            Amount = 0;
            OnFailedCallback?.Invoke( e.Message );
        }

        private void HandleUserFailed( object sender, AdFailedToLoadEventArgs e )
        {
            Debug.Log( $"Ad loading: FAILED. Message: {e.Message}" );

            Amount = 0;
            OnFailedCallback?.Invoke( e.Message );
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