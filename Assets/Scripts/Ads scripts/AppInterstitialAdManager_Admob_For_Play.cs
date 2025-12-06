using UnityEngine;
using GoogleMobileAds.Api;
using System;
// using Firebase.Analytics;
// using Firebase.Crashlytics;
using System.Collections;

public class AppInterstitialAdManager_Admob_For_Play : MonoBehaviour
{
    public static AppInterstitialAdManager_Admob_For_Play Instance { get; private set; }

#if UNITY_ANDROID
    private const string AD_UNIT_ID = "ca-app-pub-7464731741377758/1368499519"; // real Interstitial
#elif UNITY_IOS
    private const string AD_UNIT_ID = "ca-app-pub-4845920793447822/7496706129";
#else
    private const string AD_UNIT_ID = "unexpected_platform";
#endif

    private InterstitialAd _interstitialAd;
    private bool isLoadingAd = false;
    private bool isShowingAd = false;
    
    // Callbacks để giữ khi show ad
    private Action _onCloseCallback;
    private Action _successCallback;
    private Action _failCallback;

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
        // AdManager sẽ lo việc đó
    }

    public void LoadAd()
    {
        // Kiểm tra điều kiện
        if (!AdManager.IsInitialized)
        {
            Debug.LogWarning("[Interstitial] MobileAds not initialized yet");
            return;
        }

        if (!AdManager.CanShowAds())
        {
            Debug.Log("[Interstitial] Ads disabled");
            return;
        }

        if (isLoadingAd)
        {
            Debug.Log("[Interstitial] Already loading...");
            return;
        }

        // Clean up ad cũ trước khi load ad mới
        if (_interstitialAd != null)
        {
            DestroyAd();
        }

        isLoadingAd = true;
        Debug.Log("[Interstitial] Loading ad...");

        try
        {
            var adRequest = new AdRequest();

            InterstitialAd.Load(AD_UNIT_ID, adRequest, (InterstitialAd ad, LoadAdError error) =>
            {
                isLoadingAd = false;

                // Nếu có lỗi
                if (error != null)
                {
                    Debug.LogError($"[Interstitial] Load failed: {error}");
                    
                    // Retry sau 10 giây
                    StartCoroutine(RetryLoadAfterDelay(10f));
                    return;
                }

                // Nếu ad null (unexpected error)
                if (ad == null)
                {
                    Debug.LogError("[Interstitial] Unexpected error: ad is null");
                    StartCoroutine(RetryLoadAfterDelay(10f));
                    return;
                }

                // Load thành công
                _interstitialAd = ad;
                Debug.Log("[Interstitial] Ad loaded successfully: " + ad.GetResponseInfo());

                // Đăng ký event handlers
                RegisterEventHandlers(ad);
            });
        }
        catch (Exception e)
        {
            isLoadingAd = false;
            Debug.LogError($"[Interstitial] Exception during load: {e.Message}");
            //Crashlytics.LogException(e);
            
            StartCoroutine(RetryLoadAfterDelay(10f));
        }
    }

    private IEnumerator RetryLoadAfterDelay(float delay)
    {
        Debug.Log($"[Interstitial] Will retry loading in {delay} seconds...");
        yield return new WaitForSeconds(delay);
        
        if (AdManager.CanShowAds())
        {
            LoadAd();
        }
    }

    public void ShowInterstitial(Action OnClose = null, Action SuccessEvent = null, Action FailEvent = null)
    {
        // Kiểm tra điều kiện
        if (!AdManager.CanShowAds())
        {
            Debug.Log("[Interstitial] Ads disabled");
            FailEvent?.Invoke();
            OnClose?.Invoke();
            return;
        }

        if (isShowingAd)
        {
            Debug.Log("[Interstitial] Already showing an ad");
            FailEvent?.Invoke();
            return;
        }

        try
        {
            // Lưu callbacks
            _onCloseCallback = OnClose;
            _successCallback = SuccessEvent;
            _failCallback = FailEvent;

            // Kiểm tra ad có sẵn sàng không
            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                Debug.Log("[Interstitial] Showing ad...");
                isShowingAd = true;
                _interstitialAd.Show();
            }
            else
            {
                Debug.LogWarning("[Interstitial] Ad not ready yet");
                
                // Load ad mới
                LoadAd();
                
                // Gọi fail callback
                FailEvent?.Invoke();
                OnClose?.Invoke();
            }
        }
        catch (Exception e)
        {
            isShowingAd = false;
            Debug.LogError($"[Interstitial] Exception during show: {e.Message}");
            //Crashlytics.LogException(e);
            
            FailEvent?.Invoke();
            OnClose?.Invoke();
            
            // Load ad mới
            LoadAd();
        }
    }

    /// <summary>
    /// Kiểm tra xem có ad sẵn sàng để show không
    /// </summary>
    public bool IsAdReady()
    {
        return _interstitialAd != null && _interstitialAd.CanShowAd();
    }

    public void DestroyAd()
    {
        if (_interstitialAd != null)
        {
            Debug.Log("[Interstitial] Destroying ad");
            try
            {
                _interstitialAd.Destroy();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Interstitial] Destroy failed: {e.Message}");
            }
            _interstitialAd = null;
        }
    }

    public void LogResponseInfo()
    {
        if (_interstitialAd != null)
        {
            var responseInfo = _interstitialAd.GetResponseInfo();
            Debug.Log($"[Interstitial] Response Info: {responseInfo}");
        }
        else
        {
            Debug.Log("[Interstitial] No ad loaded");
        }
    }

    private void RegisterEventHandlers(InterstitialAd ad)
    {
        // Raised when the ad is estimated to have earned money
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[Interstitial] Paid: {adValue.Value} {adValue.CurrencyCode}");
            
            try
            {
                if (adValue == null) return;
                
                double value = adValue.Value * 0.000001f;

                // Parameter[] adParameters = {
                //     new Parameter("ad_platform", "admob"),
                //     new Parameter("ad_format", "interstitial"),
                //     new Parameter("currency", adValue.CurrencyCode),
                //     new Parameter("value", value),
                //     new Parameter("ad_unit_name", AD_UNIT_ID),
                //     new Parameter("ad_unit_id", AD_UNIT_ID),
                //     new Parameter("ad_source", "admob"),
                //     new Parameter("placement", "first_play"),
                //     new Parameter("revenue_precision", adValue.Precision.ToString())
                //};

                //FirebaseAnalytics.LogEvent("ad_impression", adParameters);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Interstitial] ad_impression error: {e.Message}");
                //Crashlytics.LogException(e);
            }
        };

        // Raised when an impression is recorded for an ad
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[Interstitial] Impression recorded");
        };

        // Raised when a click is recorded for an ad
        ad.OnAdClicked += () =>
        {
            Debug.Log("[Interstitial] Clicked");
        };

        // Raised when an ad opened full screen content
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[Interstitial] Full screen content opened");
            isShowingAd = true;
            
            // Gọi success callback
            _successCallback?.Invoke();
        };

        // Raised when the ad closed full screen content
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Interstitial] Full screen content closed");
            isShowingAd = false;
            
            // Gọi close callback
            _onCloseCallback?.Invoke();
            
            // Clear callbacks
            _onCloseCallback = null;
            _successCallback = null;
            _failCallback = null;
            
            // Load ad mới cho lần sau
            LoadAd();
        };

        // Raised when the ad failed to open full screen content
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[Interstitial] Full screen content failed: {error}");
            isShowingAd = false;
            
            // Gọi fail callback
            _failCallback?.Invoke();
            _onCloseCallback?.Invoke();
            
            // Clear callbacks
            _onCloseCallback = null;
            _successCallback = null;
            _failCallback = null;
            
            // Load ad mới
            LoadAd();
        };
    }

    void OnDestroy()
    {
        DestroyAd();
    }
}