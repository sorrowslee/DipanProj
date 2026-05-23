using System.Collections.Generic;
using UnityEngine;

public class GroundEffectInstance : MonoBehaviour
{
    private GroundEffectData _data;
    private LayerMask _enemyLayer;
    private SpriteRenderer _templateRenderer;
    private readonly List<SpriteRenderer> _tileRenderers = new List<SpriteRenderer>();

    private float _lifeRemaining;
    private bool _hasInfiniteLife;
    private float _animTimer;
    private int _animFrame;
    private float _damageTimer;
    private bool _initialized;

    public void Initialize(GroundEffectData data, LayerMask enemyLayer)
    {
        _data = data;
        _enemyLayer = enemyLayer;

        // Prefab 上的 SpriteRenderer 只當 sortingLayer / order / material 的範本，
        // 自身不顯示任何圖（由動態產生的 tile 子物件負責繪製）。
        _templateRenderer = GetComponent<SpriteRenderer>();
        if (_templateRenderer != null)
            _templateRenderer.sprite = null;

        BuildTiles();

        _hasInfiniteLife = _data.Duration < 0f;
        _lifeRemaining = _data.Duration;
        _damageTimer = 0f;
        _initialized = true;

        // DamageInterval == 0：生成瞬間單次爆裂；之後不再傷害。
        if (_data.DamageInterval <= 0f)
        {
            ApplyAreaDamage();
        }
    }

    private const int TileCountWarnThreshold = 500;

    /// <summary>
    /// 真實圓形掃描：以原點為中心、整數網格 (i, j)，當 tile 中心點 (i*TileSize, j*TileSize) 落在 Radius 內就保留。
    /// tile 排列嚴格貼齊網格（無半步偏移），高解析度（R/TileSize ≥ ~4）時呈現自然圓形；
    /// 低解析度（如 R=1.5、TileSize=1，直徑只跨 3 顆）會看起來偏方塊，這是 resolution 限制，
    /// 想看圓形請把 TileSize 調小（建議 ≤ Radius / 4）。
    /// 傷害判定走 OverlapCircleAll、嚴格按 Radius，與此鋪面範圍一致。
    /// </summary>
    private void BuildTiles()
    {
        if (_data == null) return;
        if (_data.AnimationSprites == null || _data.AnimationSprites.Length == 0) return;
        if (_data.Radius <= 0f) return;

        float tileSize = _data.TileSize > 0f ? _data.TileSize : 1f;
        float radius = _data.Radius;
        float radiusSqr = radius * radius;
        int maxOffset = Mathf.CeilToInt(radius / tileSize);
        Sprite firstFrame = _data.AnimationSprites[0];

        for (int j = -maxOffset; j <= maxOffset; j++)
        {
            float y = j * tileSize;
            float ySqr = y * y;
            if (ySqr > radiusSqr) continue;

            for (int i = -maxOffset; i <= maxOffset; i++)
            {
                float x = i * tileSize;
                if (x * x + ySqr > radiusSqr) continue;

                var tile = new GameObject($"Tile_{j}_{i}");
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = new Vector3(x, y, 0f);
                tile.transform.localScale = new Vector3(tileSize, tileSize, 1f);

                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = firstFrame;
                if (_templateRenderer != null)
                {
                    sr.sortingLayerID = _templateRenderer.sortingLayerID;
                    sr.sortingOrder = _templateRenderer.sortingOrder;
                    sr.sharedMaterial = _templateRenderer.sharedMaterial;
                }
                _tileRenderers.Add(sr);
            }
        }

        if (_tileRenderers.Count > TileCountWarnThreshold)
        {
            Debug.LogWarning($"GroundEffect '{_data.Name}' spawned {_tileRenderers.Count} tiles (Radius={radius}, TileSize={tileSize}); consider increasing TileSize for performance.");
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        TickAnimation();
        TickDamage();
        TickLifetime();
    }

    private void TickAnimation()
    {
        if (_data.AnimationSprites == null || _data.AnimationSprites.Length <= 1) return;
        if (_data.AnimFPS <= 0f) return;
        if (_tileRenderers.Count == 0) return;

        _animTimer += Time.deltaTime;
        float frameDuration = 1f / _data.AnimFPS;
        if (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _animFrame = (_animFrame + 1) % _data.AnimationSprites.Length;
            Sprite frame = _data.AnimationSprites[_animFrame];
            for (int i = 0; i < _tileRenderers.Count; i++)
            {
                if (_tileRenderers[i] != null)
                    _tileRenderers[i].sprite = frame;
            }
        }
    }

    private void TickDamage()
    {
        if (_data.DamageInterval <= 0f) return;

        _damageTimer += Time.deltaTime;
        if (_damageTimer >= _data.DamageInterval)
        {
            _damageTimer -= _data.DamageInterval;
            ApplyAreaDamage();
        }
    }

    private void TickLifetime()
    {
        if (_hasInfiniteLife) return;

        _lifeRemaining -= Time.deltaTime;
        if (_lifeRemaining <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyAreaDamage()
    {
        if (_data.Damage <= 0f || _data.Radius <= 0f) return;

        Vector2 center = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, _data.Radius, _enemyLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            MonsterController monster = col.GetComponent<MonsterController>();
            if (monster == null) continue;

            Vector2 hitDir = ((Vector2)monster.transform.position - center).normalized;
            monster.TakeDamage(_data.Damage, hitDir);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _data.Radius);
    }
}
