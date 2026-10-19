using UnityEngine;

namespace RuleGame.Infrastructure.Common
{
    /// <summary>
    /// 「受规则影响的动态实体」的最小契约。
    /// </summary>
    /// <remarks>
    /// <para>摩擦 / 质量这类**没有全局载体**的属性，只能逐个改实体本身：
    /// 摩擦改 <see cref="Colliders"/> 的材质，质量改 <see cref="Body"/> 的 mass。</para>
    /// <para><b>实现者必须自己登记</b>：在 <c>OnEnable</c> 调
    /// <see cref="IWorldRegistry.Register"/>，在 <c>OnDisable</c> 调
    /// <see cref="IWorldRegistry.Unregister"/>。注册表不做自动发现。</para>
    /// </remarks>
    public interface IRuleAffected
    {
        /// <summary>受力刚体。Kinematic 也要给——质量规则要改它的 mass。</summary>
        Rigidbody2D Body { get; }

        /// <summary>
        /// 参与摩擦的碰撞体。
        /// </summary>
        /// <remarks>
        /// ⚠️ **每次访问都会分配数组**。规则类应缓存结果，不要每帧读。
        /// 没有碰撞体时返回空数组，**不要返回 null**。
        /// </remarks>
        Collider2D[] Colliders { get; }
    }
}
