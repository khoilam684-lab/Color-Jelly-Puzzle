using UnityEngine;
using GoogleMobileAds.Api;
using System;
using Firebase.Analytics;
using Firebase.Crashlytics;
using System.Collections;

public class AppBannerCollapseAdManager : MonoBehaviour
{
    public static AppBannerCollapseAdManager Instance { get; private set; }

#if UNITY_ANDROID
    private const string AD_BANNER_ID = "ca-app-pub-7464731741377758/4017895341"; // real id 
   
#elif UNITY_IOS
    //private const string AD_BANNER_ID = "ca-app-pub-9674055550946724/9362464015";
#else
    private const string AD_BANNER_ID = "unexpected_platform";
#endif

    private BannerView bannerView;
    private bool isLoading = false;

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
        // KHÔNG khởi tạo MobileAds ở đây nữa
        // AdManager sẽ lo việc đó
    }

    public void CreateBannerView()
    {
        if (bannerView != null)
        {
            DestroyBannerView();
        }

        try
        {
            AdSize adSize = AdSize.Banner;   
            bannerView = new BannerView(AD_BANNER_ID, adSize, AdPosition.Bottom);
            Debug.Log("[BannerCollapse] Banner view created");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BannerCollapse] Failed to create banner: {e.Message}");
            Crashlytics.LogException(e);
        }
    }

    public void LoadAndShowBanner()
    {
        // Kiểm tra điều kiện
        if (!AdManager.CanShowAds())
        {
            Debug.Log("[BannerCollapse] Ads disabled");
            return;
        }

        if (!AdManager.IsInitialized)
        {
            Debug.LogWarning("[BannerCollapse] MobileAds not initialized yet");
            return;
        }

        if (isLoading)
        {
            Debug.Log("[BannerCollapse] Already loading...");
            return;
        }

        try
        {
            isLoading = true;
            Debug.Log("[BannerCollapse] Loading banner...");

            CreateBannerView();
            ListenToAdEvents();

            AdRequest adRequest = new AdRequest();
            adRequest.Extras.Add("collapsible", "bottom");

            bannerView.LoadAd(adRequest);
        }
        catch (Exception e)
        {
            isLoading = false;
            Debug.LogError($"[BannerCollapse] Load failed: {e.Message}");
            Crashlytics.LogException(e);
        }
    }

    public void HideBannerCollapse()
    {
        try
        {
            if (bannerView != null)
            {
                bannerView.Hide();
                Debug.Log("[BannerCollapse] Hidden");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BannerCollapse] Hide failed: {e.Message}");
            Crashlytics.LogException(e);
        }
    }

    public void ShowBannerCollapse()
    {
        if (!AdManager.CanShowAds())
        {
            return;
        }
        if (!AdManager.IsInitialized)
        {
            Debug.LogWarning("[BannerRectangle] MobileAds not initialized yet");
            return;
        }
        try
        {
            Debug.Log("[BannerCollapse] Showing...");

            if (bannerView == null)
            {
                LoadAndShowBanner();
                return;
            }

            bannerView.Show();
        }
        catch (Exception e)
        {
            Debug.LogError($"[BannerCollapse] Show failed: {e.Message}");
            Crashlytics.LogException(e);
        }
    }

    private void ListenToAdEvents()
    {
        if (bannerView == null) return;

        bannerView.OnBannerAdLoaded += () =>
        {
            isLoading = false;
            Debug.Log("[BannerCollapse] Ad loaded: " + bannerView.GetResponseInfo());
        };

        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            isLoading = false;
            Debug.LogError("[BannerCollapse] Load failed: " + error);
            
            // Retry sau 5 giây
            StartCoroutine(RetryLoad());
        };

        bannerView.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[BannerCollapse] Paid: {adValue.Value} {adValue.CurrencyCode}");

            if (adValue == null) return;
            double value = adValue.Value * 0.000001f;

            Firebase.Analytics.Parameter[] adParameters = {
                new Firebase.Analytics.Parameter("ad_source", "admob"),
                new Firebase.Analytics.Parameter("ad_format", "collapsible_banner"),
                new Firebase.Analytics.Parameter("currency","USD"),
                new Firebase.Analytics.Parameter("value", value)
            };
            FirebaseAnalytics.LogEvent("ad_impression", adParameters);
        };

        bannerView.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[BannerCollapse] Impression recorded");
        };

        bannerView.OnAdClicked += () =>
        {
            Debug.Log("[BannerCollapse] Clicked");
        };

        bannerView.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[BannerCollapse] Full screen opened");
        };

        bannerView.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[BannerCollapse] Full screen closed");
        };
    }

    private IEnumerator RetryLoad()
    {
        yield return new WaitForSeconds(5f);
        
        if (AdManager.CanShowAds() && bannerView != null)
        {
            Debug.Log("[BannerCollapse] Retrying load...");
            LoadAndShowBanner();
        }
    }

    public void DestroyBannerView()
    {
        if (bannerView != null)
        {
            Debug.Log("[BannerCollapse] Destroying banner");
            try
            {
                bannerView.Destroy();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BannerCollapse] Destroy failed: {e.Message}");
            }
            bannerView = null;
        }
        isLoading = false;
    }

    void OnDestroy()
    {
        DestroyBannerView();
    }
}