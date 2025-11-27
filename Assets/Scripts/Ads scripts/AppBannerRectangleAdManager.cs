using UnityEngine;
using GoogleMobileAds.Api;
using System;
// using Firebase.Analytics;
// using Firebase.Crashlytics;
using System.Collections;

public class AppBannerRectangleAdManager : MonoBehaviour
{
    public static AppBannerRectangleAdManager Instance { get; private set; }

#if UNITY_ANDROID
    private const string AD_BANNER_ID = "ca-app-pub-3940256099942544/6300978111"; // test
    //private const string AD_BANNER_ID = "ca-app-pub-4845920793447822/5221170220"; // id real
#elif UNITY_IOS
    private const string AD_BANNER_ID = "ca-app-pub-9674055550946724/6674868723";
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
            AdSize adSize = AdSize.MediumRectangle;
            bannerView = new BannerView(AD_BANNER_ID, adSize, AdPosition.Bottom);
            Debug.Log("[BannerRectangle] Banner view created");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BannerRectangle] Failed to create banner: {e.Message}");
            //Crashlytics.LogException(e);
        }
    }

    public void LoadAndShowBanner()
    {
        // Kiểm tra điều kiện
        if (!AdManager.CanShowAds())
        {
            Debug.Log("[BannerRectangle] Ads disabled");
            return;
        }

        if (!AdManager.IsInitialized)
        {
            Debug.LogWarning("[BannerRectangle] MobileAds not initialized yet");
            return;
        }

        if (isLoading)
        {
            Debug.Log("[BannerRectangle] Already loading...");
            return;
        }

        try
        {
            isLoading = true;
            Debug.Log("[BannerRectangle] Loading banner...");

            CreateBannerView();
            ListenToAdEvents();

            AdRequest adRequest = new AdRequest();
            
            // Rectangle banner KHÔNG dùng collapsible
            // Nếu muốn thêm collapsible thì uncomment dòng dưới:
            // adRequest.Extras.Add("collapsible", "bottom");

            bannerView.LoadAd(adRequest);
            
            // Ẩn đi ngay sau khi load, chờ gọi ShowBannerRectangle
            bannerView.Hide();
        }
        catch (Exception e)
        {
            isLoading = false;
            Debug.LogError($"[BannerRectangle] Load failed: {e.Message}");
            //Crashlytics.LogException(e);
        }
    }

    public void HideBannerRectangle()
    {
        try
        {
            if (bannerView != null)
            {
                bannerView.Hide();
                Debug.Log("[BannerRectangle] Hidden");
            }

            // Khi ẩn Rectangle thì show Collapse (nếu bạn muốn)
            if (AppBannerCollapseAdManager.Instance != null)
            {
                AppBannerCollapseAdManager.Instance.ShowBannerCollapse();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BannerRectangle] Hide failed: {e.Message}");
            //Crashlytics.LogException(e);
        }
    }

    public void ShowBannerRectangle()
    {
        if (!AdManager.CanShowAds())
        {
            Debug.Log("[BannerRectangle] Ads disabled");
            return;
        }

        try
        {
            Debug.Log("[BannerRectangle] Showing...");

            if (bannerView == null)
            {
                LoadAndShowBanner();
                // Đợi load xong rồi mới show
                StartCoroutine(WaitAndShow());
                return;
            }
            // Show Rectangle thì ẩn Collapse
            if (AppBannerCollapseAdManager.Instance != null)
            {
                AppBannerCollapseAdManager.Instance.HideBannerCollapse();
            }
            
            bannerView.Show();
        }
        catch (Exception e)
        {
            Debug.LogError($"[BannerRectangle] Show failed: {e.Message}");
            //Crashlytics.LogException(e);
        }
    }

    private IEnumerator WaitAndShow()
    {
        // Đợi banner load xong (tối đa 5 giây)
        float elapsed = 0f;
        while (isLoading && elapsed < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (bannerView != null && !isLoading)
        {
            bannerView.Show();
            
            if (AppBannerCollapseAdManager.Instance != null)
            {
                AppBannerCollapseAdManager.Instance.HideBannerCollapse();
            }
        }
    }

    private void ListenToAdEvents()
    {
        if (bannerView == null) return;

        bannerView.OnBannerAdLoaded += () =>
        {
            isLoading = false;
            Debug.Log("[BannerRectangle] Ad loaded: " + bannerView.GetResponseInfo());
        };

        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            isLoading = false;
            Debug.LogError("[BannerRectangle] Load failed: " + error);
            
            // Retry sau 5 giây
            StartCoroutine(RetryLoad());
        };

        bannerView.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[BannerRectangle] Paid: {adValue.Value} {adValue.CurrencyCode}");

            if (adValue == null) return;
            double value = adValue.Value * 0.000001f;

            Firebase.Analytics.Parameter[] adParameters = {
                new Firebase.Analytics.Parameter("ad_source", "admob"),
                new Firebase.Analytics.Parameter("ad_format", "rectangle_banner"),
                new Firebase.Analytics.Parameter("currency","USD"),
                new Firebase.Analytics.Parameter("value", value)
            };
            //FirebaseAnalytics.LogEvent("ad_impression", adParameters);
        };

        bannerView.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[BannerRectangle] Impression recorded");
        };

        bannerView.OnAdClicked += () =>
        {
            Debug.Log("[BannerRectangle] Clicked");
        };

        bannerView.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[BannerRectangle] Full screen opened");
        };

        bannerView.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[BannerRectangle] Full screen closed");
        };
    }

    private IEnumerator RetryLoad()
    {
        yield return new WaitForSeconds(5f);
        
        if (AdManager.CanShowAds() && bannerView != null)
        {
            Debug.Log("[BannerRectangle] Retrying load...");
            LoadAndShowBanner();
        }
    }

    public void DestroyBannerView()
    {
        if (bannerView != null)
        {
            Debug.Log("[BannerRectangle] Destroying banner");
            try
            {
                bannerView.Destroy();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BannerRectangle] Destroy failed: {e.Message}");
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