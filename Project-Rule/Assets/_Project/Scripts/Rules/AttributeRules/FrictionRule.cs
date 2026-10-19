using RuleGame.Infrastructure.Rules;

namespace RuleGame.Core.Rules
{
    /// <summary>
    /// 摩擦属性规则。
    /// </summary>
    /// <remarks>
    /// <para>摩擦**没有引擎全局载体**，所以只写 <see cref="IAttributeManager"/> 里的倍率，
    /// 由每个实体自己读取并改自己的材质（见 <c>RuleAffectedBody</c>）。</para>
    ///
    /// <para>⚠️ <b>实体侧的前提</b>：<c>Collider2D.sharedMaterial</c> 是共享的，缩放单实体摩擦
    /// 必须给它一份**独占**材质，而 <see cref="UnityEngine.PhysicsMaterial2D"/> 没有公开构造函数，
    /// 只能克隆一份已有的。所以实体至少要能解析出一个克隆来源
    /// （见 <c>RuleAffectedBody.ResolveMaterialSource</c>）。</para>
    ///
    /// <para>倍率实测：Unity **不会**把 <c>friction</c> 夹到 [0,1]（1.2 / 3 / 100 都原样存回），
    /// 负数会夹到 0。所以「增强」可以直接用大于 1 的倍率。</para>
    /// </remarks>
    public class FrictionRule : AttributeRuleBase
    {
        // ------------------------------------------------------------------
        //  数值：⚠️ 待策划确认
        // ------------------------------------------------------------------

        /// <summary>正常倍率。</summary>
        public const float NormalScale = 1f;

        /// <summary>失效：无摩擦力，像在冰面上。</summary>
        public const float NullifiedScale = 0f;

        /// <summary>增强：摩擦变大，移速降低，类似沼泽地。</summary>
        public const float EnhancedScale = 3f;

        /// <summary>削弱：摩擦变小。</summary>
        public const float WeakenedScale = 0.3f;

        public FrictionRule(IAttributeManager attributes) : base(attributes) { }

        /// <summary>反转：摩擦力方向反向。</summary>
        /// <remarks>
        /// <para>它**不改摩擦系数**（倍率仍是 <see cref="NormalScale"/>），只把
        /// <see cref="AttributeValues.FrictionInverted"/> 置位。</para>
        /// <para>为什么不用负倍率表达：Unity 会把 <c>PhysicsMaterial2D.friction</c> 的负值
        /// 夹到 0，负摩擦会直接退化成「失效」。</para>
        /// <para>至于「方向反过来之后实体该怎么动」，是**读这个值的实体自己的事**，
        /// 规则系统只负责把值摆对并广播 <see cref="IRuleSystem.OnRulesChanged"/>。</para>
        /// </remarks>
        protected override bool OnRevert() => Apply(NormalScale, inverted: true);

        /// <summary>失效：摩擦归零，像在冰面上。</summary>
        protected override bool OnNullify() => Apply(NullifiedScale, inverted: false);

        /// <summary>恒定：摩擦力恢复正常。</summary>
        protected override bool OnConstant() => Apply(NormalScale, inverted: false);

        /// <summary>增强：增大摩擦力。</summary>
        protected override bool OnEnhance() => Apply(EnhancedScale, inverted: false);

        /// <summary>削弱：降低摩擦力。</summary>
        protected override bool OnWeaken() => Apply(WeakenedScale, inverted: false);

        /// <summary>恢复默认摩擦。</summary>
        public override void Clear() => Apply(NormalScale, inverted: false);

        /// <summary>
        /// 一次性写两个字段。
        /// </summary>
        /// <remarks>
        /// 必须成对写：只改倍率不清 <see cref="AttributeValues.FrictionInverted"/>，
        /// 会留下「反转中」的残留状态（比如先反转再削弱，实体仍然以为方向是反的）。
        /// </remarks>
        private bool Apply(float scale, bool inverted)
        {
            Attributes.SetFrictionInverted(inverted);
            Attributes.SetFrictionScale(scale);
            return true;
        }
    }
}
