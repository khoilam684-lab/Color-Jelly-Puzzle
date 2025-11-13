using System;
using System.Collections;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

public class RemoteConfig : MonoBehaviour
{
    public static RemoteConfig Instance;

    // ==== TRẠNG THÁI & SỰ KIỆN SẴN SÀNG ====
    public static bool IsReady { get; private set; } = false;
    public static event Action OnRemoteConfigReady;
    private static readonly Queue<Action> _pendingActions = new Queue<Action>();

    // ==== CÁC KEY CHÍNH ====
    public static int ads_display       = 1;   // 1: BẬT toàn bộ ads, 0: TẮT
    public static int countDown         = 45;  // Cooldown giữa 2 lần inter (giây)
    public static int openads_enable    = 1;   // 1: BẬT App Open Ads, 0: TẮT
    public static int time_load_startapp = 3;  // ⚠️ ĐỔI DEFAULT THÀNH 3 GIÂY (hợp lý hơn 15s)

    public static int idle_ads_enable   = 1;
    public static int idle_time         = 30;
    public static int idle_min_interval = 120;
    public static int native_ads_enable = 1;
    
    // Idle interstitial (15s)
    public static int admob_15s_enable = 1;              // công tắc riêng (tùy chọn)
    public static int countDown_15s_interstitial_admob = 15; // alias cũ, vẫn hỗ trợ

    
    
    // Helper: kiểm tra tổng hợp cho AOA
    public static bool OpenAdsEnabled => (ads_display != 0) && (openads_enable != 0);

    // ==== Các key cũ để tương thích ====
    public bool  isDataFetched = false;
    public int   inter_ads_enable = 1;
    public int   banner_ads_enable = 1;
    public int   rectangle_ads_enable = 1;
    public int   ads_interval = 90;
    public int   afk_time = 15;
    public int   show_afk_ads_after_level = 2;
    public int   show_full_ads_after_level = 3;
    public int   show_iap = 1;
    


    private bool isLoadingData = false;
    private bool isRemoteData  = false;
    private Dictionary<string, object> defaults;
    private FirebaseApp app;

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

    // ===== API an toàn: chạy action sau khi RC sẵn sàng =====
    public static void RunAfterReady(Action action)
    {
        if (IsReady) { action?.Invoke(); return; }
        _pendingActions.Enqueue(action);
    }

    // ===== API coroutine: đợi RC có timeout (không kẹt app) =====
    public static IEnumerator WaitUntilReady(float timeoutSeconds, Action onTimeout = null)
    {
        float start = Time.realtimeSinceStartup;
        while (!IsReady && (Time.realtimeSinceStartup - start) < timeoutSeconds)
            yield return null;

        if (!IsReady)
        {
            Debug.LogWarning($"[RC] Timeout after {timeoutSeconds}s - using defaults");
            onTimeout?.Invoke();
        }
    }

    // ===== đánh dấu sẵn sàng + flush hàng đợi =====
    private void MarkReadyAndFlush()
    {
        if (IsReady) return;
        IsReady = true;

        Debug.Log("[RC] ✅ RemoteConfig is now READY!");

        try { OnRemoteConfigReady?.Invoke(); } catch (Exception e) { Debug.LogError($"[RC] Event error: {e}"); }

        while (_pendingActions.Count > 0)
        {
            var a = _pendingActions.Dequeue();
            try { a?.Invoke(); } catch (Exception e) { Debug.LogError($"[RC] Action error: {e}"); }
        }
    }

    // ===== public entry =====
    public void CheckLoadedRemoteData()
    {
        if (isRemoteData)
        {
            Debug.Log("[RC] Already loaded remote data");
            return;
        }
        LoadRemoteConfig();
    }

    public void LoadRemoteConfig()
    {
        if (isLoadingData)
        {
            Debug.Log("[RC] Already loading...");
            return;
        }
        isLoadingData = true;

        Debug.Log("[RC] Starting Firebase initialization...");
        FirebaseApp.LogLevel = LogLevel.Info;
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                app = FirebaseApp.DefaultInstance;
                Debug.Log("[RC] Firebase available, initializing...");
                InitializeFirebase();
            }
            else
            {
                Debug.LogError($"[RC] Firebase deps not available: {task.Result} - Using defaults");
                // vẫn cho qua dùng defaults để app không kẹt
                MarkReadyAndFlush();
            }
        });
    }

    private void InitializeFirebase()
    {
        // defaults – dùng khi chưa fetch được hoặc key chưa có trên server
        defaults = new Dictionary<string, object>
        {
            // 4 key chính
            { "ads_display",       ads_display },
            { "countDown",         countDown   },
            { "openads_enable",    openads_enable },
            { "time_load_startapp", time_load_startapp }, // ⚠️ DEFAULT = 3 giây

            // các key khác để tương thích
            { "inter_ads_enable",          inter_ads_enable },
            { "banner_ads_enable",         banner_ads_enable },
            { "rectangle_ads_enable",      rectangle_ads_enable },
            { "ads_interval",              ads_interval },
            { "afk_time",                  afk_time },
            { "show_afk_ads_after_level",  show_afk_ads_after_level },
            { "show_full_ads_after_level", show_full_ads_after_level },
            
            { "idle_ads_enable",   idle_ads_enable },
            { "idle_time",         idle_time },
            { "idle_min_interval", idle_min_interval },
            { "native_ads_enable", native_ads_enable },

            { "admob_15s_enable",                 admob_15s_enable },
            { "countDown_15s_interstitial_admob", countDown_15s_interstitial_admob }, // alias cũ
// đã có:
            { "idle_ads_enable",   idle_ads_enable },
            { "idle_time",         idle_time },
            { "idle_min_interval", idle_min_interval },

        };

        var rc = FirebaseRemoteConfig.DefaultInstance;
        rc.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
        {
            if (task.Exception != null)
            {
                Debug.LogError($"[RC] SetDefaults failed: {task.Exception}");
                MarkReadyAndFlush();
                return;
            }

            Debug.Log("[RC] Defaults set. Fetching from server...");
            FetchRemoteConfig();
        });
    }

    public void FetchRemoteConfig()
    {
        if (app == null)
        {
            Debug.LogError("[RC] Firebase not initialized - using defaults");
            MarkReadyAndFlush();
            return;
        }

        var rc = FirebaseRemoteConfig.DefaultInstance;
        
        Debug.Log("[RC] Fetching remote config...");
        rc.FetchAsync(TimeSpan.Zero).ContinueWithOnMainThread(task =>
        {
            if (task.Exception != null)
            {
                Debug.LogError($"[RC] Fetch failed: {task.Exception}");
                MarkReadyAndFlush();
                return;
            }

            FetchComplete();
        });
    }

    private void FetchComplete()
    {
        var rc = FirebaseRemoteConfig.DefaultInstance;
        var info = rc.Info;

        Debug.Log($"[RC] Fetch status: {info.LastFetchStatus}");

        if (info.LastFetchStatus == LastFetchStatus.Success)
        {
            rc.ActivateAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Exception != null)
                {
                    Debug.LogError($"[RC] Activate failed: {task.Exception}");
                    MarkReadyAndFlush();
                    return;
                }

                ReflectProperties();
                isDataFetched = true;
                isRemoteData  = true;

                Debug.Log($"[RC] ✅ SUCCESS! Values loaded:");
                Debug.Log($"  - ads_display: {ads_display}");
                Debug.Log($"  - openads_enable: {openads_enable}");
                Debug.Log($"  - countDown: {countDown}s");
                Debug.Log($"  - time_load_startapp: {time_load_startapp}s");

                MarkReadyAndFlush();
            });
        }
        else
        {
            Debug.LogWarning($"[RC] Fetch not success: {info.LastFetchStatus} - Using defaults");
            isDataFetched = true;
            isRemoteData  = false;

            MarkReadyAndFlush();
        }
    }

    private void ReflectProperties()
    {
        var rc = FirebaseRemoteConfig.DefaultInstance;

        // 4 key quan trọng
        ads_display        = (int)rc.GetValue("ads_display").DoubleValue;
        countDown          = (int)rc.GetValue("countDown").DoubleValue;
        openads_enable     = (int)rc.GetValue("openads_enable").DoubleValue;
        time_load_startapp = (int)rc.GetValue("time_load_startapp").DoubleValue;
        
        idle_ads_enable   = (int)rc.GetValue("idle_ads_enable").DoubleValue;
        idle_time         = (int)rc.GetValue("idle_time").DoubleValue;
        idle_min_interval = (int)rc.GetValue("idle_min_interval").DoubleValue;
        native_ads_enable = (int)rc.GetValue("native_ads_enable").DoubleValue;

// clamp cho an toàn
        idle_time         = Mathf.Max(10, idle_time);
        idle_min_interval = Mathf.Max(30, idle_min_interval);


        // ⚠️ VALIDATION: Đảm bảo time_load_startapp hợp lý
        if (time_load_startapp < 1)
        {
            Debug.LogWarning($"[RC] time_load_startapp too small ({time_load_startapp}), using 2s");
            time_load_startapp = 2;
        }
        else if (time_load_startapp > 10)
        {
            Debug.LogWarning($"[RC] time_load_startapp too large ({time_load_startapp}s), clamping to 5s");
            time_load_startapp = 5;
        }

        // Fallback nếu chưa tạo 'countDown' trên Firebase
        if (countDown <= 0)
        {
            int fallback = (int)rc.GetValue("ads_interval").DoubleValue;
            if (fallback > 0) countDown = fallback;
            else countDown = 45; // ultimate fallback
        }

        // các key khác
        inter_ads_enable          = (int)rc.GetValue("inter_ads_enable").DoubleValue;
        banner_ads_enable         = (int)rc.GetValue("banner_ads_enable").DoubleValue;
        rectangle_ads_enable      = (int)rc.GetValue("rectangle_ads_enable").DoubleValue;
        afk_time                  = (int)rc.GetValue("afk_time").DoubleValue;
        show_afk_ads_after_level  = (int)rc.GetValue("show_afk_ads_after_level").DoubleValue;
        show_full_ads_after_level = (int)rc.GetValue("show_full_ads_after_level").DoubleValue;
        
        // công tắc + thông số mới
        idle_ads_enable   = (int)rc.GetValue("idle_ads_enable").DoubleValue;
        idle_time         = (int)rc.GetValue("idle_time").DoubleValue;
        idle_min_interval = (int)rc.GetValue("idle_min_interval").DoubleValue;
        native_ads_enable = (int)rc.GetValue("native_ads_enable").DoubleValue;
        admob_15s_enable  = (int)rc.GetValue("admob_15s_enable").DoubleValue;

// alias cũ -> ghi đè idle_time nếu có cấu hình
        countDown_15s_interstitial_admob = (int)rc.GetValue("countDown_15s_interstitial_admob").DoubleValue;
        if (countDown_15s_interstitial_admob > 0)
            idle_time = countDown_15s_interstitial_admob;

// clamp an toàn
        idle_time         = Mathf.Max(5,  idle_time);
        idle_min_interval = Mathf.Max(30, idle_min_interval);

    }
}