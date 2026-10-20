#if UNITY_EDITOR
using UnityEngine;
using RuleGame.Gameplay.Enemy;

namespace RuleGame.Development.Testing
{
    /// <summary>
    /// 测试用：给 Boss 换一个血量总量，不改共享资产、不改生产代码。
    /// </summary>
    /// <remarks>
    /// <para>挂在测试场景里的 Boss 上，Inspector 填 <see cref="_health"/>，进 Play 生效。
    /// <b>≤ 0 表示不改</b>（保持 <c>EnemyDefinition.Health</c>）。</para>
    /// <para>改的是运行时的 <c>Definition.Health</c>，所以<b>阶段阈值（×2/3、×1/3）会一起变</b>
    /// —— 只改 <c>EntityStatus</c> 的血池而不改这个值，蝙蝠 / 闹钟 Boss 会立刻按旧阈值切阶段。</para>
    /// <para>⚠️ <c>EnemyDefinition</c> 是共享资产：同一场景里共用同一份 Definition 的其它实体
    /// 也会被一起改。测试场景通常只有一个 Boss，问题不大。退出 Play / 销毁时自动还原。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class BossHealthOverride : MonoBehaviour
    {
        [Tooltip("留空自动取同物体上的 EnemyActor。")]
        [SerializeField] private EnemyActor _actor;

        [Tooltip("血量总量。≤ 0 = 不改，沿用 EnemyDefinition.Health。")]
        [SerializeField] private float _health;

        /// <summary>血量总量。≤ 0 表示不覆盖。面板在编辑期改的就是这个字段。</summary>
        public float Health
        {
            get => _health;
            set => _health = value;
        }

        private float _original;
        private bool _applied;

        private void Awake()
        {
            if (_actor == null) _actor = GetComponent<EnemyActor>();
        }

        // Start 而不是 Awake：EnemyActor.Awake 里已经 InitializeVitals 过，这里要覆盖它。
        private void Start() => Apply();

        private void OnDestroy() => Restore();

        /// <summary>把血量总量改成 Inspector 里填的值。</summary>
        public void Apply()
        {
            if (_applied || _health <= 0f) return;
            if (_actor == null || _actor.Definition == null || _actor.Status == null) return;

            _original = _actor.Definition.Health;
            _actor.Definition.Health = _health;
            _actor.Status.InitializeVitals(_health, _actor.Status.EnergyMax);
            _applied = true;
        }

        /// <summary>还原成进来时的值（Play 退出时 Unity 本来也会丢弃运行时改动，这里是兜底）。</summary>
        public void Restore()
        {
            if (!_applied || _actor == null || _actor.Definition == null) return;

            _actor.Definition.Health = _original;
            _applied = false;
        }
    }
}
#endif
