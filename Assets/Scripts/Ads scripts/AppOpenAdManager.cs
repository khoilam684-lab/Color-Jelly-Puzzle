using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

// ====== BẬT nếu bạn dùng Firebase ======
using Firebase.Analytics;
#if FIREBASE_CRASHLYTICS
using Firebase.Crashlytics;
#endif
// =======================================

public class AppOpenAdManager : MonoSingleton<AppOpenAdManager>
{
#if UNITY_ANDROID
    
    private const string AD_UNIT_ID = "ca-app-pub-3940256099942544/9257395921";// Test ID của Google 
    //private const string AD_UNIT_ID = "ca-app-pub-7775816915507213/7222058088";// ID real
#elif UNITY_IOS
    private const string AD_UNIT_ID = "ca-app-pub-7775816915507213/7222058088";
#else
    private const string AD_UNIT_ID = "unused";
#endif

    AppOpenAd appOpenAd;
    DateTime  loadTimeUtc;
    bool      isLoaded;
    bool      isShowing;
    bool      firstShowDone;

    public override void Awake()
    {
        base.Awake();

        // RequestConfiguration kiểu mới (9.x → KHÔNG dùng Builder)
        try
        {
            var config = new RequestConfiguration
            {
                TestDeviceIds = new List<string> { AdRequest.TestDeviceSimulator }
            };
            MobileAds.SetRequestConfiguration(config);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AOA] SetRequestConfiguration failed: " + e.Message);
        }

        MobileAds.Initialize(_ =>
        {
            AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
            LoadAppOpenAd();                    // chỉ load
        });
    }

    // Ad “tươi” trong 4h theo khuyến nghị
    private bool IsAdFresh => appOpenAd != null && isLoaded &&
                              (DateTime.UtcNow - loadTimeUtc).TotalHours < 4.0;

    // ======================= LOAD =======================
    public void LoadAppOpenAd()
    {
        if (appOpenAd != null)
        {
            appOpenAd.Destroy();
            appOpenAd = null;
        }

        isLoaded = false;

        var request = new AdRequest();
        AppOpenAd.Load(AD_UNIT_ID, request, (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError("[AOA] Load failed: " + error);
                return;
            }

            appOpenAd   = ad;
            loadTimeUtc = DateTime.UtcNow;
            isLoaded    = true;

            RegisterEventHandlers(ad);

            // Nếu muốn auto-show lần đầu vào game:
            if (!firstShowDone) Invoke(nameof(ShowOnFirstOpenSafe), 0.2f);
            Debug.Log("[AOA] Loaded.");
        });
    }
    // ====================================================

    private void ShowOnFirstOpenSafe()
    {
        if (!firstShowDone)
        {
            ShowAppOpenAd(reason: "first_open");
            firstShowDone = true;
        }
    }

    // ======================= SHOW =======================
    public void ShowAppOpenAd(string reason = "manual")
    {
        if (isShowing) { Debug.Log("[AOA] already showing"); return; }

        if (!IsAdFresh)
        {
            Debug.Log("[AOA] ad not ready/fresh → reload");
            LoadAppOpenAd();
            return;
        }

        if (!appOpenAd.CanShowAd())
        {
            Debug.Log("[AOA] CanShowAd=false → reload");
            LoadAppOpenAd();
            return;
        }

        Debug.Log($"[AOA] Show (reason={reason})");
        isShowing = true;
        appOpenAd.Show();
    }
    // ====================================================

    // ======== GẮN SỰ KIỆN CỦA APP OPEN AD =========
    private void RegisterEventHandlers(AppOpenAd ad)
    {
        ad.OnAdPaid += HandlePaidEvent;   // 👈 QUAN TRỌNG: log doanh thu tại đây!

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AOA] Impression recorded");
            // Không log revenue ở đây để tránh double-count
        };

        ad.OnAdClicked += () => Debug.Log("[AOA] Click");

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AOA] Opened");
            isShowing = true;
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AOA] Closed");
            isShowing = false;
            isLoaded  = false;    // ad đã tiêu thụ xong
            LoadAppOpenAd();      // load ad mới
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("[AOA] Fullscreen failed: " + error);
            isShowing = false;
            isLoaded  = false;
            LoadAppOpenAd();
        };
    }
    // =====================================================

    // ========== LOG DOANH THU (OnAdPaid) ==========
    private void HandlePaidEvent(AdValue adValue)
    {
        Debug.LogFormat("[AOA] Paid event → currency:{0}, micro:{1}",
            adValue?.CurrencyCode, adValue?.Value);

        try
        {
            if (adValue == null) return;

            // AdMob trả MICRO (1e-6) → đổi sang đơn vị tiền thật
            double value    = adValue.Value * 0.000001d;
            string currency = string.IsNullOrEmpty(adValue.CurrencyCode) ? "USD" : adValue.CurrencyCode;

            // GA4: ad_impression với value (double) + currency
            var adParameters = new[]
            {
                new Parameter("ad_source", "admob"),
                new Parameter("ad_format", "app_open"),
                new Parameter("currency",  currency),
                new Parameter("value",     value),
            };
            FirebaseAnalytics.LogEvent("ad_impression", adParameters);

            // (Tuỳ chọn) AppsFlyer Ad Revenue — chỉ chạy khi bạn có SDK + define
            #if APPSFLYER_ADREVENUE
            var dic = new System.Collections.Generic.Dictionary<string,string>
            {
                { "ad_format", "app_open" }
            };
            AppsFlyerAdRevenue.logAdRevenue(
                "Admob",
                AppsFlyerAdRevenueMediationNetworkType.GoogleAdMob,
                value,
                currency,
                dic
            );
            #endif
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            #if FIREBASE_CRASHLYTICS
            Crashlytics.LogException(e);
            #endif
        }
    }
    // ===============================================

    // App quay lại foreground → show nếu đã qua lần đầu
    private void OnAppStateChanged(AppState state)
    {
        if (state != AppState.Foreground) return;
        Invoke(nameof(TryShowOnForeground), 0.3f);
    }

    private void TryShowOnForeground()
    {
        if (!firstShowDone) return;             // lần đầu đã show ở ShowOnFirstOpenSafe
        ShowAppOpenAd(reason: "foreground");
    }

    private void OnDestroy()
    {
        AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
        if (appOpenAd != null) appOpenAd.Destroy();
    }
}
