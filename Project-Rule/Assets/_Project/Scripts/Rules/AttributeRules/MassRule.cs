using RuleGame.Infrastructure.Rules;

namespace RuleGame.Core.Rules
{
    /// <summary>
    /// 质量属性规则。
    /// </summary>
    /// <remarks>
    /// <para>质量**没有引擎全局载体**，所以只写 <see cref="IAttributeManager"/> 里的系数，
    /// 由每个实体自己读取并应用（见 <c>RuleAffectedBody</c>）。
    /// 规则类**不遍历注册表** —— 这样规则生效后新生成的实体也天然拿到最新值。</para>
    ///
    /// <para>⚠️ <b>反转为什么用标志位而不是倍率</b>：
    /// 规则矩阵里「反转 = 重的变轻，轻的变重」是**逐实体**的语义，
    /// 而 <see cref="AttributeValues.MassScale"/> 是全局倍率，表达不了。
    /// 所以走 <see cref="AttributeValues.MassInverted"/>，由实体按参考质量取倒数。</para>
    /// </remarks>
    public class MassRule : AttributeRuleBase
    {
        // ------------------------------------------------------------------
        //  数值：⚠️ 待策划确认，目前按 GravityRule 的同一套量级取值
        // ------------------------------------------------------------------

        /// <summary>正常倍率。</summary>
        public const float NormalScale = 1f;

        /// <summary>增强：质量变为 3 倍。</summary>
        public const float EnhancedScale = 3f;

        /// <summary>削弱：质量变为 0.3 倍。</summary>
        public const float WeakenedScale = 0.3f;

        /// <summary>失效：质量变得极轻（0.05 倍）。</summary>
        public const float NullifiedScale = 0.05f;

        public MassRule(IAttributeManager attributes) : base(attributes) { }

        /// <summary>反转：重的变轻，轻的变重。</summary>
        protected override bool OnRevert() => Apply(NormalScale, inverted: true);

        /// <summary>失效：重量都变得极轻。</summary>
        protected override bool OnNullify() => Apply(NullifiedScale, inverted: false);

        /// <summary>恒定：质量恢复正常。</summary>
        protected override bool OnConstant() => Apply(NormalScale, inverted: false);

        /// <summary>增强：质量变大。</summary>
        protected override bool OnEnhance() => Apply(EnhancedScale, inverted: false);

        /// <summary>削弱：质量变轻。</summary>
        protected override bool OnWeaken() => Apply(WeakenedScale, inverted: false);

        /// <summary>恢复默认质量。</summary>
        public override void Clear() => Apply(NormalScale, inverted: false);

        /// <summary>
        /// 一次性写两个字段。
        /// </summary>
        /// <remarks>
        /// 必须成对写：只改倍率不清标志位，会留下「反转中」的残留状态。
        /// </remarks>
        private bool Apply(float scale, bool inverted)
        {
            Attributes.SetMassInverted(inverted);
            Attributes.SetMassScale(scale);
            return true;
        }
    }
}
