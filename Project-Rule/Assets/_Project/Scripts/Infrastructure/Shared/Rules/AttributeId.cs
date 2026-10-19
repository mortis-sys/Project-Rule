namespace RuleGame.Infrastructure.Rules
{
    /// <summary>
    /// 规则系统的 6 个属性（已砍掉「空间」「方向」「关系」）。
    /// </summary>
    /// <remarks>
    /// ⚠️ 枚举顺序即规则维护表的槽位下标，同时也是 <c>RuleDef</c> 资产里
    /// <c>Attribute</c> 字段序列化出来的**整数**。**不要调整顺序，也不要插入新值**
    /// —— 那会让已存档的规则与已有资产对不上槽位。
    /// <para>
    /// 「关系」于 2026-10-12 **彻底删除**（而非保留占位）：当时的伤害资产会随伤害系统重写，
    /// 且尚无存档格式需要兼容，所以这次删除是免费的。以后再删属性没这么便宜 ——
    /// 先确认没有资产 / 存档引用它，并准备好重挂受影响的 <c>RuleDef</c>。
    /// </para>
    /// </remarks>
    public enum AttributeId
    {
        Gravity,    // 重力
        Time,       // 时间
        Friction,   // 摩擦
        Mass,       // 质量
        Damage,     // 伤害/生命
        Causality,  // 因果
    }
}
