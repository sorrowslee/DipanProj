using UnityEngine;

/// <summary>
/// 讓「互動星星」等世界標示不被氛圍後處理（AtmosphereBlit）壓暗：
/// 用一台**疊在主相機之上、不做任何後處理**的相機，把它們重畫一次（暗場景也永遠可見）。
///
/// 作法：星星放在 <b>InteractOverlay</b> 這個 Unity Layer（見 InteractMarker）。本相機每幀對齊 Camera.main
/// 的視角/投影，只畫這一層（cullingMask），depth = 主相機+1、clearFlags = Depth（只疊上去、不清色、不套 Atmosphere）；
/// 同時把主相機的 cullingMask 去掉這層（避免主相機又畫一份被壓暗的）。
///
/// 自動生成、跨場景常駐、零接線（同 AtmosphereController 模式）。
/// 若專案沒加 InteractOverlay 這層 → 本相機停用、星星退回原本行為（被壓暗），不會壞。
/// </summary>
[DisallowMultipleComponent]
public class OverlayCameraController : MonoBehaviour
{
    public const string LayerName = "InteractOverlay";
    public static int OverlayLayer { get; private set; } = -1;

    static OverlayCameraController _instance;
    Camera _cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (_instance != null) return;
        OverlayLayer = LayerMask.NameToLayer(LayerName);
        if (OverlayLayer < 0)
        {
            Debug.LogWarning($"[OverlayCamera] 找不到 Layer「{LayerName}」——互動星星穿透黑暗停用（請到 Tags and Layers 加這層）。");
            return;
        }
        var go = new GameObject("[InteractOverlayCamera]");
        _instance = go.AddComponent<OverlayCameraController>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        _cam = gameObject.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.clearFlags = CameraClearFlags.Depth;     // 只疊在畫面上，不清色（保留主相機的畫面）
        _cam.cullingMask = 1 << OverlayLayer;          // 只畫互動星星那層
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        _cam.useOcclusionCulling = false;
        gameObject.tag = "Untagged";                   // 不搶 MainCamera
    }

    void LateUpdate()
    {
        var main = Camera.main;
        if (main == null || _cam == null) { if (_cam != null) _cam.enabled = false; return; }
        _cam.enabled = true;

        // 主相機別再畫這層（否則會多畫一份被壓暗的）——交給本相機。
        main.cullingMask &= ~(1 << OverlayLayer);

        // 對齊主相機的視角與投影，讓星星和場景精準重合。
        transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        _cam.orthographic = main.orthographic;
        _cam.orthographicSize = main.orthographicSize;
        _cam.nearClipPlane = main.nearClipPlane;
        _cam.farClipPlane = main.farClipPlane;
        _cam.rect = main.rect;
        _cam.depth = main.depth + 1;                   // 疊在主相機（含 Atmosphere 後處理）之上
    }
}
