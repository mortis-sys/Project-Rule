using System.Collections.Generic;

namespace RuleGame.Infrastructure.Common
{
    /// <summary>
    /// 受规则影响的动态实体登记表。
    /// </summary>
    /// <remarks>
    /// <para><b>用途</b>：摩擦 / 质量遍历（逐个改碰撞体材质与刚体质量）、快照采集。</para>
    /// <para>⚠️ <b>不要每帧 <c>FindObjectsOfType</c></b> —— 注册表就是为替代它而存在的。</para>
    /// <para><b>谁维护</b>：P1。实体自己调用 <see cref="Register"/> / <see cref="Unregister"/>。</para>
    /// </remarks>
    public interface IWorldRegistry
    {
        /// <summary>
        /// 登记一个实体。
        /// </summary>
        /// <remarks>重复登记同一实例会被忽略；<c>null</c> 被忽略。</remarks>
        void Register(IRuleAffected entity);

        /// <summary>注销一个实体。没登记过或 <c>null</c> 时静默忽略。</summary>
        void Unregister(IRuleAffected entity);

        /// <summary>
        /// 当前所有已登记、且**未被销毁**的实体。
        /// </summary>
        /// <remarks>
        /// <para>返回的是**只读视图**，不是快照副本——调用 <see cref="Register"/> /
        /// <see cref="Unregister"/> 会改变它。**不要在遍历过程中增删**。</para>
        /// <para>已销毁但没来得及注销的实体会被自动剔除，不需要调用方处理。</para>
        /// </remarks>
        IReadOnlyList<IRuleAffected> All { get; }
    }
}
