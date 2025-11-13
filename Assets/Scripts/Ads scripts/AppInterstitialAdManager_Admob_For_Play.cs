using UnityEngine;
using UnityEngine.UI;
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;
using Firebase.Analytics;
using Firebase.Crashlytics;
using System.Security.Cryptography;

public class AppInterstitialAdManager_Admob_For_Play : MonoSingleton<AppInterstitialAdManager_Admob_For_Play>
{
#if UNITY_ANDROID
    private const string AD_BANNER_ID = "ca-app-pub-3940256099942544/6300978111"; // test Fixed Sized Banner
    //private const string AD_BANNER_ID = "ca-app-pub-7775816915507213/7432034972"; // id real
#elif UNITY_IOS
    private const string AD_BANNER_ID = "ca-app-pub-4845920793447822/7496706129";
#else
    private const string AD_BANNER_ID = "unexpected_platform";
#endif
    private static AppInterstitialAdManager_Admob_For_Play instance;

    private InterstitialAd _interstitialAd;

    public void LoadAd()
    {
        // Clean up the old ad before loading a new one.
        if (_interstitialAd != null)
        {
            DestroyAd();
        }

        Debug.LogError("Loading interstitial ad for play.");

        // Create our request used to load the ad.
        var adRequest = new AdRequest();

        InterstitialAd.Load(AD_BANNER_ID, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            // If the operation failed with a reason.
            if (error != null)
            {
                Debug.LogError("Interstitial ad failed to load an ad with error : " + error);
                return;
            }
            // If the operation failed for unknown reasons.
            // This is an unexpected error, please report this bug if it happens.
            if (ad == null)
            {
                Debug.LogError("Unexpected error: Interstitial load event fired with null ad and null error.");
                return;
            }

            // The operation completed successfully.
            Debug.Log("Interstitial ad loaded with response : " + ad.GetResponseInfo());
            _interstitialAd = ad;

            // Register to ad events to extend functionality.
            RegisterEventHandlers(ad);
        });
    }

    public void ShowInterstitial(Action OnClose = null, Action SuccessEvent = null, Action FailEvent = null)
    {
        try
        {
            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                _interstitialAd.Show();
                //AppmetricaTracking.AdTracking(TypeAdTracking.INTERSTITIAL, "SHOW");
                _interstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    //AppmetricaTracking.AdTracking(TypeAdTracking.INTERSTITIAL, "CLOSE");
                    Debug.Log("Interstitial Admob ad full screen content closed.");
                    OnClose?.Invoke();
                    //AdvertisementManager.Instance.forceAdsCountdown = RemoteConfig.forceAdsCountdown;
                    //AdvertisementManager.Instance.StartCoroutine(AdvertisementManager.Instance.WaitForShowAds());
                    LoadAd();
                };
                _interstitialAd.OnAdFullScreenContentOpened += () =>
                {
                    Debug.LogError("Interstitial Admob ad full screen content opened.");
                    SuccessEvent?.Invoke();
                };
                _interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Debug.LogError("Interstitial Admob ad fail.");
                    FailEvent?.Invoke();
                };
            }
            else
            {
                Debug.LogError("Interstitial Admob ad is not ready yet.");
                LoadAd();
                FailEvent?.Invoke();
                OnClose?.Invoke();
            }
        }
        catch (Exception e)
        {
            Crashlytics.LogException(e);
            Crashlytics.Log("Exception occurred in AdmobInterstitialAd_show 100%");
        }
    }
    public void DestroyAd()
    {
        if (_interstitialAd != null)
        {
            Debug.Log("Destroying interstitial ad.");
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }
    }

    public void LogResponseInfo()
    {
        if (_interstitialAd != null)
        {
            var responseInfo = _interstitialAd.GetResponseInfo();
            UnityEngine.Debug.Log(responseInfo);
        }
    }

    private void RegisterEventHandlers(InterstitialAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Interstitial admob paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
            try
            {
                if (adValue == null) return;
                double value = adValue.Value * 0.000001f;

                Parameter[] adParameters =
{
                    new Parameter("ad_platform", "admob"),
                    new Parameter("ad_format", "interstitial"),
                    new Parameter("currency", adValue.CurrencyCode),
                    new Parameter("value", value),
                    new Parameter("ad_unit_name", AD_BANNER_ID),
                    new Parameter("ad_unit_id", AD_BANNER_ID),
                    new Parameter("ad_source", "admob"),
                    new Parameter("placement", "first_play"),
                    new Parameter("revenue_precision", adValue.Precision.ToString())
                };

                FirebaseAnalytics.LogEvent("ad_impression", adParameters);
            }
            catch (Exception e)
            {
                Crashlytics.LogException(e);
                Crashlytics.Log("Exception occurred in ad_impression interstitial for play");
            }
        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Interstitial admob recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("Interstitial admob was clicked.");
        };
        // Raised when an ad opened full screen content.
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Interstitial admob full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Interstitial admob full screen content closed.");
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Interstitial admob failed to open full screen content with error : "
                + error);
        };
    }

}