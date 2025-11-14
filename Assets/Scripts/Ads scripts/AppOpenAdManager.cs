using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using Firebase.Analytics;
#if FIREBASE_CRASHLYTICS
using Firebase.Crashlytics;
#endif

public class AppOpenAdManager : MonoBehaviour
{
    public static AppOpenAdManager Instance { get; private set; }

#if UNITY_ANDROID
    private const string AD_UNIT_ID = "ca-app-pub-3940256099942544/9257395921";// Test ID
    //private const string AD_UNIT_ID = "ca-app-pub-7775816915507213/7222058088";// ID real
#elif UNITY_IOS
    private const string AD_UNIT_ID = "ca-app-pub-7775816915507213/7222058088";
#else
    private const string AD_UNIT_ID = "unused";
#endif

    private AppOpenAd appOpenAd;
    private DateTime loadTimeUtc;
    private bool isLoaded;
    private bool isShowing;
    private bool firstShowDone;
    private bool isLoadingAd;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // KHÔNG khởi tạo MobileAds ở đây
        // Chỉ đăng ký event app state
        AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
        
        // Load ad sẽ được gọi từ AdManager sau khi init xong
    }

    private bool IsAdFresh => appOpenAd != null && isLoaded &&
                              (DateTime.UtcNow - loadTimeUtc).TotalHours < 4.0;

    public void LoadAppOpenAd()
    {
        // Kiểm tra điều kiện
        if (!AdManager.IsInitialized)
        {
            Debug.LogWarning("[AOA] MobileAds not initialized yet");
            return;
        }

        if (!RemoteConfig.OpenAdsEnabled)
        {
            Debug.Log("[AOA] App Open Ads disabled via RemoteConfig");
            return;
        }

        if (isLoadingAd)
        {
            Debug.Log("[AOA] Already loading...");
            return;
        }

        if (appOpenAd != null)
        {
            appOpenAd.Destroy();
            appOpenAd = null;
        }

        isLoaded = false;
        isLoadingAd = true;

        Debug.Log("[AOA] Loading ad...");
        
        var request = new AdRequest();
        AppOpenAd.Load(AD_UNIT_ID, request, (ad, error) =>
        {
            isLoadingAd = false;

            if (error != null || ad == null)
            {
                Debug.LogError($"[AOA] Load failed: {error}");
                return;
            }

            appOpenAd = ad;
            loadTimeUtc = DateTime.UtcNow;
            isLoaded = true;

            RegisterEventHandlers(ad);
            Debug.Log("[AOA] Ad loaded successfully");

            // Show lần đầu với delay
            if (!firstShowDone)
            {
                Invoke(nameof(ShowOnFirstOpenSafe), 0.5f);
            }
        });
    }

    private void ShowOnFirstOpenSafe()
    {
        if (!firstShowDone && RemoteConfig.OpenAdsEnabled)
        {
            ShowAppOpenAd(reason: "first_open");
            firstShowDone = true;
        }
    }

    public void ShowAppOpenAd(string reason = "manual")
    {
        if (!AdManager.CanShowAds() || !RemoteConfig.OpenAdsEnabled)
        {
            Debug.Log("[AOA] Ads disabled");
            return;
        }

        if (isShowing)
        {
            Debug.Log("[AOA] Already showing");
            return;
        }

        if (!IsAdFresh)
        {
            Debug.Log("[AOA] Ad not ready/fresh → reload");
            LoadAppOpenAd();
            return;
        }

        if (!appOpenAd.CanShowAd())
        {
            Debug.Log("[AOA] CanShowAd=false → reload");
            LoadAppOpenAd();
            return;
        }

        Debug.Log($"[AOA] Showing ad (reason={reason})");
        isShowing = true;
        
        try
        {
            appOpenAd.Show();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AOA] Show failed: {e.Message}");
            isShowing = false;
            LoadAppOpenAd();
        }
    }

    private void RegisterEventHandlers(AppOpenAd ad)
    {
        ad.OnAdPaid += HandlePaidEvent;

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AOA] Impression recorded");
        };

        ad.OnAdClicked += () => Debug.Log("[AOA] Clicked");

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AOA] Opened");
            isShowing = true;
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AOA] Closed");
            isShowing = false;
            isLoaded = false;
            LoadAppOpenAd();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[AOA] Fullscreen failed: {error}");
            isShowing = false;
            isLoaded = false;
            LoadAppOpenAd();
        };
    }

    private void HandlePaidEvent(AdValue adValue)
    {
        Debug.Log($"[AOA] Paid: {adValue?.Value} {adValue?.CurrencyCode}");

        try
        {
            if (adValue == null) return;

            double value = adValue.Value * 0.000001d;
            string currency = string.IsNullOrEmpty(adValue.CurrencyCode) ? "USD" : adValue.CurrencyCode;

            var adParameters = new[]
            {
                new Parameter("ad_source", "admob"),
                new Parameter("ad_format", "app_open"),
                new Parameter("currency", currency),
                new Parameter("value", value),
            };
            FirebaseAnalytics.LogEvent("ad_impression", adParameters);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
#if FIREBASE_CRASHLYTICS
            Crashlytics.LogException(e);
#endif
        }
    }

    private void OnAppStateChanged(AppState state)
    {
        if (state != AppState.Foreground) return;
        if (!firstShowDone) return;

        Invoke(nameof(TryShowOnForeground), 0.3f);
    }

    private void TryShowOnForeground()
    {
        if (firstShowDone && RemoteConfig.OpenAdsEnabled)
        {
            ShowAppOpenAd(reason: "foreground");
        }
    }

    void OnDestroy()
    {
        AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
        if (appOpenAd != null)
        {
            appOpenAd.Destroy();
        }
    }
}