using RuleGame.Infrastructure.Rules;

namespace RuleGame.Core.Rules
{
    /// <summary>
    /// 伤害 / 生命属性规则。
    /// </summary>
    /// <remarks>
    /// <para>伤害<b>没有引擎全局载体</b>，所以写进 <see cref="IAttributeManager"/>（见 §4.5）——
    /// 战斗系统再从那里读，玩家与敌人共用同一份值。</para>
    ///
    /// <para>只改伤害相关的四个字段，<b>不动摩擦 / 质量</b>：否则会误伤同时生效的其它规则。</para>
    ///
    /// <para><b>边界（重要）</b>：本类只负责把值摆对并让 <c>RuleSystem</c> 广播。
    /// 「值拿到之后怎么算」是伤害处理模块 / 承受方的事——
    /// 尤其是<b>恒定（挂账）</b>：账本、净额结算、结算时机都不在规则系统里。</para>
    /// </remarks>
    public class DamageRule : AttributeRuleBase
    {
        /// <summary>增强倍率。</summary>
        public const float EnhancedScale = 2f;

        /// <summary>削弱倍率。</summary>
        public const float WeakenedScale = 0.5f;

        /// <remarks>伤害需要属性管理器写值，构造函数注入（决策 2026-10-12）。</remarks>
        public DamageRule(IAttributeManager attributes) : base(attributes) { }

        /// <summary>反转：攻击变回血，回血变攻击。</summary>
        protected override bool OnRevert() => Apply(inverted: true);

        /// <summary>失效：攻击与回血都失效。</summary>
        protected override bool OnNullify() => Apply(disabled: true);

        /// <summary>
        /// 恒定：伤害挂账。
        /// </summary>
        /// <remarks>
        /// <para>规则矩阵：「受到的伤害不立即扣血，先记账；规则结束时一次性结算。
        /// 窗口内的治疗可抵消挂账（按净额结算）」。</para>
        /// <para>本类只置位 <see cref="AttributeValues.DamageDeferred"/>。
        /// 账本与结算归伤害处理模块 / 承受方——账本是逐实体的，
        /// 放不进属性规则类。</para>
        /// </remarks>
        protected override bool OnConstant() => Apply(deferred: true);

        /// <summary>增强：攻防数值放大。</summary>
        protected override bool OnEnhance() => Apply(scale: EnhancedScale);

        /// <summary>削弱：攻防数值缩小。</summary>
        protected override bool OnWeaken() => Apply(scale: WeakenedScale);

        /// <summary>到期 / 被覆盖 / 重开关卡时恢复默认伤害参数。</summary>
        public override void Clear() => Apply();

        /// <summary>
        /// 一次性写全伤害的四个字段。
        /// </summary>
        /// <remarks>
        /// <para>必须<b>写全</b>：只改自己要用的那个会留下上一个关系的残留
        /// （先反转再增强，回血标志会留着）。</para>
        /// <para>默认参数就是「全部正常」，所以 <c>Clear()</c> 直接 <c>Apply()</c>。</para>
        /// </remarks>
        private bool Apply(float scale = 1f, bool inverted = false,
                           bool disabled = false, bool deferred = false)
        {
            Attributes.SetDamageScale(scale);
            Attributes.SetDamageInverted(inverted);
            Attributes.SetDamageDisabled(disabled);
            Attributes.SetDamageDeferred(deferred);
            return true;
        }
    }
}
