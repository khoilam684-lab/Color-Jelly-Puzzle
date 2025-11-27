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
        try
        {
            _idleSeconds = Mathf.Max(5f, RemoteConfig.countDown_15s_interstitial_admob);
        }
        catch
        {
            _idleSeconds = defaultIdleSeconds;
        }

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
        // CHỈ lo công tắc riêng 15s; ads_display đã ở AdManager.CanShowAds()
        try
        {
            int enable15s = 1;
            try { enable15s = RemoteConfig.admob_15s_enable; } catch { }
            return enable15s != 0;
        }
        catch
        {
            // RC lỗi thì coi như bật (vì vẫn bị chặn bởi CanShowAds)
            return true;
        }
    }

    bool IsUserInteracting()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            return true;

        // nếu cần kiểm tra UI:
        // if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
        return false;
    }

    void TryShow()
    {
        _waitingOrShowing = true;

        Action onClose = () =>
        {
            _timer = 0f;
            _waitingOrShowing = false;
        };

        Action onFail = () =>
        {
            _waitingOrShowing = false;
        };

        // User vừa mua NoAds / RC vừa tắt ads
        if (!AdManager.CanShowAds())
        {
            onClose.Invoke();
            return;
        }

        var interMgr = AppInterstitialAdManager_Admob_For_Play.Instance;
        if (interMgr == null)
        {
            Debug.LogWarning("[15s Interstitial] AppInterstitialAdManager_Admob_For_Play.Instance == null");
            onFail.Invoke();
            onClose.Invoke();
            return;
        }

        interMgr.ShowInterstitial(onClose, null, onFail);
    }
}
