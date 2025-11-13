using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class Interstitial_AdManager_For_15s : MonoSingleton<Interstitial_AdManager_For_15s>
{
    [Tooltip("Mặc định; sẽ bị RC ghi đè nếu có")]
    public float defaultIdleSeconds = 15f;

    public enum STATE_15s_ADMOD { None, SceneGamePlay, SceneStartGame, SceneHomeDesign }
    public STATE_15s_ADMOD state_15s_Admob = STATE_15s_ADMOD.SceneStartGame;

    float _idleSeconds;        // lấy từ RC
    float _timer;
    bool  _waitingOrShowing;   // chặn gọi nhiều lần

    void Start()
    {
        // lấy từ RC; nếu không có thì dùng mặc định
        try { _idleSeconds = Mathf.Max(5f, RemoteConfig.countDown_15s_interstitial_admob); }
        catch { _idleSeconds = defaultIdleSeconds; }

        _timer = 0f;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!FeatureEnabledByRC()) return;
        if (state_15s_Admob == STATE_15s_ADMOD.None) return;

        if (IsUserInteracting()) _timer = 0f;

        _timer += Time.unscaledDeltaTime;

        if (!_waitingOrShowing && _timer >= _idleSeconds)
            TryShow();
    }

    bool FeatureEnabledByRC()
    {
        // công tắc tổng + (tùy chọn) công tắc riêng cho 15s
        try
        {
            int adsDisplay = RemoteConfig.ads_display;
            int enable15s = 1;
            try { enable15s = RemoteConfig.admob_15s_enable; } catch { }
            return adsDisplay != 0 && enable15s != 0;
        }
        catch { return true; }
    }

    bool IsUserInteracting()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0) return true;
        // nếu cần kiểm tra UI:
        // if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
        return false;
    }

    void TryShow()
    {
        _waitingOrShowing = true;

        Action onClose = () => { _timer = 0f; _waitingOrShowing = false; };
        Action onFail  = () => { _waitingOrShowing = false; };

        // // nếu có hệ thống RemoveAds tự bạn, check tại đây
        // if (HasRemoveAds())
        // {
        //     onClose.Invoke();
        //     return;
        // }

        AppInterstitialAdManager_Admob_For_Play.Instance.ShowInterstitial(onClose, null, onFail);
    }

    // bool HasRemoveAds()
    // {
    //     try { return SolitaireTripeaks.AuxiliaryData.Get().BuyRemoveAds; }
    //     catch { return false; }
    // }
}
