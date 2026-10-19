using System;
using System.Collections.Generic;
using RuleGame.Infrastructure.Rules;

namespace RuleGame.Core.Rules
{
    /// <summary>
    /// 规则维护表：6 个固定槽位，每属性一个。
    /// 同时是**规则剩余时间的唯一持有者**。
    /// </summary>
    public class RuleTable : IRuleTable
    {
        /// <summary>槽位数 = AttributeId 的成员数。</summary>
        private const int SlotCount = 6;

        /// <summary>
        /// 「永久」的剩余时间哨兵值。
        /// </summary>
        /// <remarks>
        /// <see cref="Set"/> 会把任何 <c>&lt;= 0</c> 的时长归一成它。
        /// 见 <see cref="Set"/> 的说明——不能直接用 0，否则第一帧就会到期。
        /// </remarks>
        public const float PermanentRemaining = -1f;

        private readonly ActiveRule?[] _slots = new ActiveRule?[SlotCount];
        private readonly Dictionary<AttributeId, ActiveRule> _readonlyView = new();

        public bool IsActive(AttributeId attr) => _slots[(int)attr].HasValue;

        public bool IsActive(AttributeId attr, RelationId rel)
            => _slots[(int)attr] is { } r && r.Relation == rel;

        public ActiveRule? GetSlot(AttributeId attr) => _slots[(int)attr];

        /// <remarks>
        /// ⚠️ 返回的是**复用的内部字典**，每次访问都会重填。
        /// 不要跨帧持有，也不要嵌套访问。
        /// </remarks>
        public IReadOnlyDictionary<AttributeId, ActiveRule> Slots
        {
            get
            {
                _readonlyView.Clear();
                for (int i = 0; i < SlotCount; i++)
                    if (_slots[i] is { } r) _readonlyView[(AttributeId)i] = r;
                return _readonlyView;
            }
        }

        // ---------------------------------------------------------------
        //  写入：只由 RuleSystem 调用
        // ---------------------------------------------------------------

        /// <summary>写入一个槽位。</summary>
        /// <remarks>
        /// ⚠️ <paramref name="duration"/> <c>&lt;= 0</c> 会归一成 <see cref="PermanentRemaining"/>。
        /// 不能直接存 0：<see cref="Tick"/> 判永久用的是 <c>Remaining &lt; 0</c>，
        /// 存 0 会在第一帧被减成负数然后立刻到期——与 <c>RuleDef.Duration</c>
        /// 上写的「&lt;= 0 表示关卡内永久」自相矛盾。
        /// </remarks>
        public void Set(AttributeId attr, RelationId rel, float duration, RuleSource source)
        {
            _slots[(int)attr] = new ActiveRule
            {
                Relation  = rel,
                Remaining = duration <= 0f ? PermanentRemaining : duration,
                Source    = source,
            };
        }

        public void Clear(AttributeId attr) => _slots[(int)attr] = null;

        public void ClearAll()
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = null;
        }

        // ---------------------------------------------------------------
        //  快照读写
        // ---------------------------------------------------------------

        public ActiveRule?[] SnapshotSlots()
        {
            var copy = new ActiveRule?[SlotCount];
            Array.Copy(_slots, copy, SlotCount);
            return copy;
        }

        public void RestoreSlots(ActiveRule?[] slots)
        {
            if (slots == null || slots.Length != SlotCount)
            {
                UnityEngine.Debug.LogError($"[RuleTable] 槽位数组长度不符：期望 {SlotCount}，收到 {slots?.Length ?? -1}");
                return;
            }
            Array.Copy(slots, _slots, SlotCount);
        }

        // ---------------------------------------------------------------
        //  计时：规则系统统一 Tick
        // ---------------------------------------------------------------

        /// <summary>递减所有有时限的槽位；到期的回调 <paramref name="onExpired"/>。</summary>
        public void Tick(float deltaTime, Action<AttributeId> onExpired)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] is not { } rule) continue;
                if (rule.Remaining < 0f) continue;          // 永久规则跳过

                rule.Remaining -= deltaTime;
                if (rule.Remaining <= 0f)
                {
                    _slots[i] = null;
                    onExpired((AttributeId)i);
                }
                else
                {
                    _slots[i] = rule;
                }
            }
        }
    }
}
