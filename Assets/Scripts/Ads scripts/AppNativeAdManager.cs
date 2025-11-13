using UnityEngine;
using UnityEngine.UI;
using GoogleMobileAds.Api;
using System;
using Firebase.Analytics;
#if FIREBASE_CRASHLYTICS
using Firebase.Crashlytics;
#endif

public class AppNativeAdManager : MonoBehaviour
{
#if UNITY_ANDROID
    //private const string AD_NATIVE_ID = "ca-app-pub-3940256099942544/2247696110"; // Test
    private const string AD_NATIVE_ID = "ca-app-pub-7775816915507213/3278943644"; // id real
#elif UNITY_IOS
    private const string AD_NATIVE_ID = "ca-app-pub-9674055550946724/9529188500";
#else
    private const string AD_NATIVE_ID = "unused";
#endif

    private static AppNativeAdManager instance;

    private NativeAd nativeAd;
    private bool _showWhenLoaded = false; // Cờ để trì hoãn hiển thị panel
    private bool nativeAdLoaded = false;

    [SerializeField] private GameObject adNativePanel;
    [SerializeField] private GameObject adNativePanelLoadFailed;
    [SerializeField] private RawImage adIcon;
    [SerializeField] private RawImage adChoices;
    [SerializeField] private Text adHeadline;
    [SerializeField] private Text adCallToAction;
    [SerializeField] private Text adAdvertiser;

    public static AppNativeAdManager Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // ✅ Ẩn mặc định khi vào game
        adNativePanel?.SetActive(false);
        adNativePanelLoadFailed?.SetActive(false);
    }

    private void Start()
    {
        // ✅ Chỉ load sẵn, KHÔNG hiển thị
        MobileAds.Initialize(_ => RequestNativeAd());
    }

    private void OnDestroy()
    {
        // 3. Destroy ad cũ khi MonoBehaviour bị hủy để tránh rò bộ nhớ
        if (nativeAd != null) nativeAd.Destroy();
    }

    private void Update()
    {
        if (!nativeAdLoaded) return;
        nativeAdLoaded = false;

        // 5. Null-check khi gán texture/text để tránh NRE nếu prefab thiếu.
        // Cập nhật nội dung quảng cáo
        if (adIcon) adIcon.texture = nativeAd.GetIconTexture();
        if (adChoices) adChoices.texture = nativeAd.GetAdChoicesLogoTexture();
        if (adHeadline) adHeadline.text = nativeAd.GetHeadlineText();
        if (adCallToAction) adCallToAction.text = nativeAd.GetCallToActionText();
        if (adAdvertiser) adAdvertiser.text = nativeAd.GetAdvertiserText();

        // Đăng ký view tracking
        if (adIcon) nativeAd.RegisterIconImageGameObject(adIcon.gameObject);
        if (adChoices) nativeAd.RegisterAdChoicesLogoGameObject(adChoices.gameObject);
        if (adHeadline) nativeAd.RegisterHeadlineTextGameObject(adHeadline.gameObject);
        if (adCallToAction) nativeAd.RegisterCallToActionGameObject(adCallToAction.gameObject);
        if (adAdvertiser) nativeAd.RegisterAdvertiserTextGameObject(adAdvertiser.gameObject);

        // ❌ Không tự hiện panel, chờ bạn bật thủ công
        if (adNativePanel) adNativePanel.SetActive(false);
    }

    #region Revenue Logging
    private void LogRevenue_Native(AdValue adValue)
    {
        try
        {
            if (adValue == null) return;

            // 2. Micro → đơn vị tiền: dùng 0.000001d (double)
            double value = adValue.Value * 0.000001d; 
            // 1. Currency: dùng adValue.CurrencyCode (fallback "USD" nếu null/empty)
            string currency = string.IsNullOrEmpty(adValue.CurrencyCode) ? "USD" : adValue.CurrencyCode;

            var p = new[]
            {
                new Firebase.Analytics.Parameter("ad_source", "admob"),
                new Firebase.Analytics.Parameter("ad_format", "native"),
                new Firebase.Analytics.Parameter("currency",  currency),
                new Firebase.Analytics.Parameter("value",     value),
            };
            FirebaseAnalytics.LogEvent("ad_impression", p);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            // Vứt try catch vào toàn bộ những chỗ cần push impression 
            #if FIREBASE_CRASHLYTICS
            Crashlytics.LogException(e);
            Crashlytics.Log("Exception occurred in ad_impression native");
            #endif
        }
    }
    #endregion

    #region Native Ad
    public void RequestNativeAd()
    {
        // f) Khi bắt đầu load, ẩn panel lỗi
        if (adNativePanelLoadFailed) adNativePanelLoadFailed.SetActive(false);

        var adLoader = new AdLoader.Builder(AD_NATIVE_ID)
            .ForNativeAd()
            .Build();

        adLoader.OnNativeAdLoaded += HandleNativeAdLoaded;
        adLoader.OnAdFailedToLoad += HandleNativeAdFailedToLoad;

        var request = new AdRequest();
        adLoader.LoadAd(request);
    }

    private void HandleNativeAdLoaded(object sender, NativeAdEventArgs args)
    {
        Debug.Log("[Native] Loaded");

        // 3. Destroy ad cũ nếu có
        if (nativeAd != null) nativeAd.Destroy();

        nativeAd = args.nativeAd;
        nativeAdLoaded = true;

        // 4. Ẩn panel lỗi nếu trước đó có hiện
        if (adNativePanelLoadFailed) adNativePanelLoadFailed.SetActive(false);

        // Gắn sự kiện doanh thu
        nativeAd.OnPaidEvent += (object s, AdValueEventArgs e) => LogRevenue_Native(e.AdValue); // ✅


        // Tự động hiển thị nếu cờ _showWhenLoaded được bật
        if (_showWhenLoaded)
        {
            _showWhenLoaded = false;
            adNativePanel?.SetActive(true);
        }
    }

    private void HandleNativeAdFailedToLoad(object sender, AdFailedToLoadEventArgs args)
    {
        Debug.LogError("[Native] Failed: " + args.LoadAdError.GetMessage());
        adNativePanelLoadFailed?.SetActive(true);
    }
    #endregion

    // ✅ Hàm thủ công để bật / tắt hiển thị
    public void ToggleNative(bool show, bool requestIfNeeded = false)
    {
        if (show)
        {
            if (nativeAd != null)
            {
                adNativePanel?.SetActive(true);
                adNativePanelLoadFailed?.SetActive(false);
                return;
            }

            if (requestIfNeeded)
            {
                _showWhenLoaded = true; // Chờ load xong rồi mới bật
                RequestNativeAd();
                return;
            }

            Debug.LogWarning("[Native] No ad loaded yet!");
            return;
        }
        else
        {
            adNativePanel?.SetActive(false);
        }
    }
}
